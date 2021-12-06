Imports System.Diagnostics.Contracts
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Runtime.ConstrainedExecution
Imports System.Security
Imports System.Text
Imports System.Text.RegularExpressions

Imports ShanXingTech

Namespace ShanXingTech.Net2

    Public Class BdVerifier
        Inherits BaiduNetdisk

#Region "事件区"
        Public Event CheckReport As EventHandler(Of CheckReportEventArgs)
        Public Event Browsing As EventHandler(Of BrowsingEventArgs)
#End Region

#Region "字段区"
        Private ReadOnly m_BdVerifierConf As BdVerifierConf
        Private m_Cts As Threading.CancellationTokenSource
#End Region

#Region "属性区"

        ''' <summary>
        ''' 文件目录以...排序
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Order() As OrderMode
            Get
                Return m_BdVerifierConf.Order
            End Get
        End Property


        ''' <summary>
        ''' 降序否
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property Desc() As Boolean
            Get
                Return m_BdVerifierConf.Desc
            End Get
        End Property
#End Region

#Region "构造函数区"
        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="bdVerifierConf">用于实例化类<see cref="BdVerifier"/>的配置类</param>
        Public Sub New(ByRef bdVerifierConf As BdVerifierConf)
            MyBase.New(bdVerifierConf)
            m_BdVerifierConf = bdVerifierConf
            m_Cts = New Threading.CancellationTokenSource

            If m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic Is Nothing Then
                m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic = New Dictionary(Of String, ShareResultCacheInfo)
            End If
        End Sub
#End Region

#Region "函数区"
        ''' <summary>
        ''' 更改文件展示的顺序（以什么排序）
        ''' </summary>
        ''' <param name="order"></param>
        Public Sub ChangeOrder(ByVal order As OrderMode)
            m_BdVerifierConf.Order = order
        End Sub

        ''' <summary>
        ''' 更改文件展示的顺序（升序或降序）
        ''' </summary>
        ''' <param name="desc"></param>
        Public Sub ChangeDesc(ByVal desc As Boolean)
            m_BdVerifierConf.Desc = desc
        End Sub

        ''' <summary>
        ''' 检测文件是否包含违规文件
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        Public Async Function CheckAsync(ByVal path As List(Of PathInfo)) As Task
            If path.Count = 0 Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.Unknow, "未选中任何项"))
                Return
            End If

            m_Cts = New Threading.CancellationTokenSource

            ' 根据用户设置
            ' 全部分享一次，有违规文件再展开检测
            Dim contain = Await ContainIllegalFileAsync(path)
            If m_Cts.IsCancellationRequested Then Return

            If LegalOptions.Yes = contain.Legal Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, "无违规文件，无需展开检测"))
                Return
            End If

            If path.Count = 1 Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, contain.Path))

                ' 只有一个并且为文件夹
                If path(0).IsDir Then GoTo Expand

                ' 只有一个并且为文件不需要再检测，退出即可
                Return
            Else
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, contain.Path))

                ' 分享失败并且需要分享的全部为文件
                If contain.Fs_Id = -1 AndAlso Not path.Exists(Function(p) p.IsDir) Then
                    GoTo Expand

                    Return
                End If
            End If

            Dim sb As New StringBuilder
            For Each file In path
                If file.Id <> contain.Fs_Id Then Continue For

                Dim pathArr = contain.Path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
                If pathArr.Length = 1 Then
                    sb.Append(file.FullPath)
                ElseIf pathArr.Length > 1 Then
                    sb.Append("/"c).Append(file.FullPath)
                    For i = 1 To pathArr.Length - 1
                        sb.Append("/"c).Append(pathArr(i))
                    Next
                    Exit For
                End If
            Next

            If sb.Length = 0 Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, "此分享包含违规文件，但合并违规路径失败"))
                Return
            End If

            Dim illegalPath = sb.Replace("\"c, "/"c).ToString
            RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, $"‘{illegalPath}’ 包含违规文件，需要展开检测"))

            path = Await ExpandDirectoryAsync(illegalPath)

Expand:
            If m_Cts.IsCancellationRequested Then Return

            ' 展开检测
            Dim checkRst = Await ExpandCheckAsync(path)

            ' 如果展开检测检测不到违规文件，需要给提示用户（暂时还找不到好方法可以100%检测到违规文件）
            If checkRst.ContainIllegalFile = LegalOptions.Yes AndAlso checkRst.Path.Count = 0 Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(contain.Legal, "此分享包含违规文件，但软件无法检测出违规文件"))
            End If
        End Function

        ''' <summary>
        ''' 判断 <paramref name="path"/> 是否包含违规文件
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        Private Async Function ContainIllegalFileAsync(ByVal path As List(Of PathInfo)) As Task(Of (Legal As LegalOptions, Path As String, Fs_Id As Long))
            Dim pathConcat = $"路径：【
{vbTab}{String.Join("、" & Environment.NewLine & vbTab, path.Select(Function(f) f.FullPath))}
】{Environment.NewLine}"
            Dim pathConcatWithSlash = String.Join("@@", path.Select(Function(f) f.FullPath))

            Dim fs_Ids_Md5 As String
            Dim shareInfo As ShareResultCacheInfo
            Dim shareRst As HttpResponse
            If path.Count = 1 Then
                Dim id = path(0).Id.ToStringOfCulture

                fs_Ids_Md5 = id.GetMD5Value

#Disable Warning BC42030 ' 在为变量赋值之前，变量已通过引用传递
                If m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic.TryGetValue(fs_Ids_Md5, shareInfo) Then
#Enable Warning BC42030 ' 在为变量赋值之前，变量已通过引用传递
                    GoTo Verify
                Else
                    shareInfo = New ShareResultCacheInfo(id)
                    shareRst = Await ShareAsync(id, m_BdVerifierConf.VerifyExpirationDate, m_BdVerifierConf.SharePrivatePassword)
                    Await Task.Delay(2000)
                End If
            Else
                Dim fileIds = path.Select(Function(f) f.Id.ToStringOfCulture).OrderBy(Function(id) id)
                fs_Ids_Md5 = String.Join(String.Empty, fileIds).GetMD5Value

                If m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic.TryGetValue(fs_Ids_Md5, shareInfo) Then
                    GoTo Verify
                Else
                    shareInfo = New ShareResultCacheInfo(String.Join(", ", fileIds))
                    shareRst = Await ShareMultiAsync(fileIds.ToList, m_BdVerifierConf.VerifyExpirationDate, m_BdVerifierConf.SharePrivatePassword)
                    Await Task.Delay(2000)
                End If
            End If

            If m_Cts.IsCancellationRequested Then
                Return (LegalOptions.Unknow, pathConcat & "任务取消", -1)
            End If

            Dim root As ShareResultEntity.Root
            Try
                root = MSJsSerializer.Deserialize(Of ShareResultEntity.Root)(shareRst.Message)
            Catch ex As Exception
                Logger.WriteLine(ex, shareRst.Message,,,)
            End Try

#Disable Warning BC42104 ' 在为变量赋值之前，变量已被使用
            If root Is Nothing Then
#Enable Warning BC42104 ' 在为变量赋值之前，变量已被使用
                Return (LegalOptions.Unknow, pathConcat & "获取分享结果失败", -1)
            End If

            shareInfo.ErrorNo = root.errno
            shareInfo.ErrorDescription = root.show_msg
            shareInfo.Time = Now
            shareInfo.ExpirationTime = shareInfo.Time.AddDays(m_BdVerifierConf.VerifyExpirationDate)
            shareInfo.Paths = pathConcatWithSlash

            If 0 <> shareInfo.ErrorNo Then
                m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic.Add(fs_Ids_Md5, shareInfo)
                Return (LegalOptions.No, pathConcat & shareInfo.ErrorDescription, -1)
            End If

            ' 检测是否合法（不可访问就是分享失败）
            ' 不可访问直接展开检测
            shareInfo.Link = root.link.TryUnescape

            m_BdVerifierConf.Fs_Ids_Md5_ShareLinkDic.Add(fs_Ids_Md5, shareInfo)

Verify：
            If shareInfo.ErrorNo <> 0 Then
                Return (LegalOptions.No, pathConcat & shareInfo.ErrorDescription, -1)
            End If

            If m_Cts.IsCancellationRequested Then
                Return (LegalOptions.Unknow, pathConcat & "任务取消", -1)
            End If

            Return Await VerifyShareFileAsync(shareInfo, pathConcat)
        End Function

        ''' <summary>
        ''' 检验分享文件的有效性。注：这里是查看自己分享的文件，不需要提取码
        ''' </summary>
        ''' <param name="shareInfo">分享信息</param>
        ''' <returns>文件合法返回True,文件（包含）违规返回False</returns>
        Public Overloads Async Function VerifyShareFileAsync(ByVal shareInfo As ShareResultCacheInfo, ByVal pathConcat As String) As Task(Of (Legal As LegalOptions, Path As String, Fs_Id As Long))
            Try
                Dim rst = Await TryDoGetAsync(shareInfo.Link)

                If Not rst.Success Then Return (LegalOptions.Unknow, shareInfo.Link, -1)

                ' 获取目录
                Dim shareData = rst.Message.GetFirstMatchValue("locals.mset\((.*?)\)")
                If shareData.IsNullOrEmpty Then
                    Dim notFound = rst.Message.Contains("share_nofound")

                    ' 登录和未登录时页面能看到的特征字符
                    Dim containLegalChars = notFound OrElse rst.Message.Contains("请输入提取码") OrElse rst.Message.Contains("失效时间") OrElse rst.Message.Contains("过期时间")

                    If containLegalChars Then
                        Return (LegalOptions.No, pathConcat, -1)
                    End If
                End If

                Dim root = MSJsSerializer.Deserialize(Of ShareFileEntity.Root)(shareData)

                ' 顶层目录为文件夹并且此时检测分享的链接正是文件夹
                If root.errno <> 0 Then
                    shareInfo.ErrorNo = root.errno
                    shareInfo.ErrorDescription = GetShareErrorNoDescription(root.errno)
                    Return (LegalOptions.No, pathConcat & shareInfo.ErrorDescription, -1)
                End If

                For Each f In root.file_list
                    Try
                        ' 能浏览到分享的文件说明度盘暂时没有提示包含违规文件
                        ' /sharelink655338460-3261719981/VB6SP6中文企业版 序列号为全1.ISO
                        ' 貌似是读盘的bug，某个文件的  f.path并不是具体路径，而是只有 .后缀名，比如 .zip
                        ' 因此，我们需要自己拼接，生成一个 path
                        Dim fPath = $"{f.parent_path.UrlDecode}/{f.server_filename}"
                        Dim browseRst = Await BrowseDirectoryAsync(fPath, root.uk, root.shareid)
                        If browseRst.Success Then Continue For

                        Dim path = browseRst.Path
                        Dim match = Regex.Match(path, "sharelink\d+\-(\d+)+(/.*)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
                        If match.Success Then
                            ' match.Groups(1).Value = Fs_Id 为上级目录 Fs_Id
                            Dim msg = $"{pathConcat}之  【{match.Groups(2).Value}】{Environment.NewLine}问题：{browseRst.Message}"
                            Return (LegalOptions.No, msg, match.Groups(1).Value.ToLongOfCulture)
                        Else
                            Return (LegalOptions.No, path, -1)
                        End If

                    Catch ex As Exception
                        Logger.WriteLine(ex)
                    End Try
                Next

                If root.errno = 0 Then
                    Return (LegalOptions.Yes, String.Empty, -1)
                Else
                    shareInfo.ErrorNo = root.errno
                    shareInfo.ErrorDescription = GetShareErrorNoDescription(root.errno)
                    Return (LegalOptions.No, pathConcat & shareInfo.ErrorDescription, -1)
                End If
            Catch ex As TaskCanceledException
                ' do nothing
            End Try

            Return (LegalOptions.Yes, String.Empty, -1)
        End Function

        ''' <summary>
        ''' 浏览分享文件夹
        ''' </summary>
        ''' <param name="dir"></param>
        ''' <param name="uk"></param>
        ''' <param name="shareId"></param>
        ''' <returns></returns>
        Private Async Function BrowseDirectoryAsync(ByVal dir As String, ByVal uk As Integer, ByVal shareId As Long) As Task(Of (Success As Boolean, Message As String, Path As String))
            RaiseEvent Browsing(Nothing, New BrowsingEventArgs(dir))

            Dim getRst = Await GetShareDirInfoAsync(dir, uk, shareId)
            If Not getRst.Success Then Return (False, "发送分享请求失败", dir)
            Dim root = MSJsSerializer.Deserialize(Of ShareDirectoryEntity.Root)(getRst.Message)

            If root.errno <> 0 Then
                Return (False, root.show_msg, dir)
            End If

            For Each l In root.list
                If 1 <> l.isdir Then Continue For

                Dim rst = Await BrowseDirectoryAsync(l.path, uk, shareId)
                Await Task.Delay(618)

                If Not rst.Success Then
                    Return rst
                End If
            Next

            Return (True, String.Empty, String.Empty)
        End Function

        ' base code from :https://referencesource.microsoft.com/#mscorlib/system/array.cs,c9d30a83673759f0
        ' public static int BinarySearch(Array array, int index, int length, Object value, IComparer comparer)
        ''' <summary>
        ''' 采用二分法检测
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        Private Async Function ExpandCheckAsync(ByVal path As List(Of PathInfo)) As Task(Of (ContainIllegalFile As LegalOptions, Path As List(Of PathInfo)))
            If path Is Nothing Then
                Throw New ArgumentNullException(NameOf(path))
            End If
            If path.Count = 0 Then
                Return (LegalOptions.Unknow, New List(Of PathInfo))
            End If

            ' 只有一个文件而且是文件夹则直接展开目录
            If path.Count = 1 AndAlso path(0).IsDir Then
                path = Await ExpandDirectoryAsync(path(0).FullPath)
            End If
            '
            If path.Count = 0 Then
                Return (LegalOptions.Unknow, New List(Of PathInfo))
            End If

            ' 先处理文件，再处理文件夹，处理文件的速度比文件夹要快，因为文件夹可能需要展开
            Dim files = path.FindAll(Function(f) Not f.IsDir)
            Dim chkRst = Await InternalBinaryCheckAsync(files)
            If chkRst.Legal = LegalOptions.No Then
                Return (LegalOptions.No, chkRst.Path)
            End If

            If m_Cts.IsCancellationRequested Then
                Return (LegalOptions.Unknow, chkRst.Path)
            End If

            Dim directorys = path.FindAll(Function(f) f.IsDir)
            Return Await InternalBinaryCheckAsync(directorys)
        End Function

        Private Async Function InternalBinaryCheckAsync(ByVal path As List(Of PathInfo)) As Task(Of (Legal As LegalOptions, Path As List(Of PathInfo)))
            Dim lo As Integer = 0
            Dim hi As Integer = lo + path.Count - 1
            Dim pathSlice As List(Of PathInfo)

            While lo <= hi
                ' i might overflow if lo and hi are both large positive numbers. 
                Dim i As Integer = GetMedian(lo, hi)

                ' 执行分享并获取分享结果
                pathSlice = If(i = 0 OrElse i = path.Count - 1, path.GetRange(lo， 1), GetPathSlice(path, lo， i))
                Dim contain = Await ContainIllegalFileAsync(pathSlice)
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(contain.Legal, contain.Path))

                If contain.Path.Contains("分享失败：分享次数超出限制") Then
                    Return (LegalOptions.No, pathSlice)
                End If

                If m_Cts.IsCancellationRequested Then
                    Return (LegalOptions.Unknow, pathSlice)
                End If

                If LegalOptions.No = contain.Legal Then
                    If pathSlice.Count <> 1 Then
                        hi = i
                        Continue While
                    End If

                    If pathSlice(0).IsDir Then
                        ' 进入目录
                        Dim subDirPathId = Await ExpandDirectoryAsync(pathSlice(0).FullPath)
                        Return Await ExpandCheckAsync(subDirPathId)
                    Else
                        Return (LegalOptions.No, pathSlice)
                    End If
                ElseIf LegalOptions.Yes = contain.Legal Then
                    lo = i + 1
                End If
            End While

#Disable Warning BC42104 ' 在为变量赋值之前，变量已被使用
            Return (LegalOptions.Unknow, If(pathSlice, New List(Of PathInfo)))
#Enable Warning BC42104 ' 在为变量赋值之前，变量已被使用
        End Function

        Private Function GetPathSlice(ByVal path As List(Of PathInfo), ByVal index As Integer, ByVal offset As Integer) As List(Of PathInfo)
            Dim l As New List(Of PathInfo)
            If index = offset Then
                l.Add(path(index))
                Return l
            End If

            For i = index To offset
                l.Add(path(i))
            Next

            Return l
        End Function

        Private Async Function ExpandDirectoryAsync(ByVal fullPath As String) As Task(Of List(Of PathInfo))
            Dim dir = If(fullPath.Chars(0) = "/"c,
            fullPath.Replace("\"c, "/"c),
            "/" & fullPath.Replace("\"c, "/"c))

            ' 根据路径获取文件夹的 fs Id
            Dim root = Await GetDirInfoAsync(dir)
            If root Is Nothing Then
                Return New List(Of PathInfo)
            End If

            If root.errno <> 0 Then
                Return New List(Of PathInfo)
            End If

            Dim path As New List(Of PathInfo)
            For Each d In root.list
                Dim item = New PathInfo(d.fs_id, d.isdir = 1, d.path) With {
                    .FullPath = d.path
                }
                path.Add(item)
            Next
            Return path
        End Function

        ''' <summary>
        ''' 取中间值
        ''' </summary>
        ''' <param name="low"></param>
        ''' <param name="hi"></param>
        ''' <returns></returns>
        Private Function GetMedian(low As Integer, hi As Integer) As Integer
            ' Note both may be negative, if we are dealing with arrays w/ negative lower bounds.
            Contract.Requires(low <= hi)
            Contract.Assert(hi - low >= 0, "Length overflow!")
            Return low + ((hi - low) >> 1)
        End Function

        <SecurityCritical()>
        <ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)>
        <MethodImpl(MethodImplOptions.InternalCall)>
        Private Function TrySZBinarySearch(sourceArray As Array, sourceIndex As Integer, count As Integer, value As Object, <Runtime.InteropServices.Out()> ByRef retVal As Integer) As Boolean
        End Function

        ''' <summary>
        ''' 获取网盘某个目录信息
        ''' </summary>
        ''' <param name="dir">目录路径。不需要编码,如 '/软件包' </param>
        ''' <returns></returns>
        Public Async Function GetShareDirInfoAsync(ByVal dir As String, ByVal uk As Integer, ByVal shareId As Long) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/share/list?uk={uk}&shareid={shareId}&order=other&desc=1&showempty=0&web=1&page=1&num=100&dir={dir.UrlEncode}&t=0.42717085533817023&channel=chunlei&web=1&app_id=250528&bdstoken=null&logid={GetBase64LogId()}&clienttype=0"

            Return Await TryDoGetAsync(url)
        End Function

        ''' <summary>
        ''' 取消任务
        ''' </summary>
        Public Sub Cancel()
            m_Cts.Cancel()
        End Sub

#End Region
    End Class
End Namespace