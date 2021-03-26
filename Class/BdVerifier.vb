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

#Region "构造函数区"
        Public Sub New(ByRef cookies As CookieContainer, bdsToken As String, bdUSS As String)
            MyBase.New(cookies, bdsToken, bdUSS)
        End Sub
#End Region

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

            ' 全部分享一次，有违规文件再展开检测
            Dim contain = Await ContainIllegalFileAsync(path)

            If LegalOptions.Yes = contain.Legal Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, "无违规文件，无需展开检测"))
                Return
            End If

            Dim sb As New StringBuilder
            For Each file In path
                If file.Id <> CLng(contain.Fs_Id) Then Continue For

                Dim pathArr = contain.Path.Split({"/"c}, StringSplitOptions.RemoveEmptyEntries)
                If pathArr.Length = 1 Then
                    sb.Append(contain.Path)
                ElseIf pathArr.Length > 1 Then
                    sb.Append("/").Append(file.FullPath)
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

            Dim illegalPath = sb.ToString.Replace("\", "/")
            RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.No, $"‘{illegalPath}’   包含违规文件，需要展开检测"))

            path = Await GetPathsIdAsync(illegalPath)

            ' 展开检测
            Dim checkRst = Await BinaryCheckAsync(path)

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
        Private Async Function ContainIllegalFileAsync(ByVal path As List(Of PathInfo)) As Task(Of (Legal As LegalOptions, Path As String, Fs_Id As String))
            Dim pathFullpath = $"文件：【{Environment.NewLine}{String.Join("、" & Environment.NewLine, path.Select(Function(f) f.FullPath))}{Environment.NewLine}】{Environment.NewLine}"

            Dim shareRst As HttpResponse
            If path.Count = 1 Then
                shareRst = Await ShareAsync(path(0).Id.ToStringOfCulture, ShareExpirationDate.OneDay, "ptyo")
            Else
                Dim fileIds = path.Select(Function(f) f.Id.ToStringOfCulture)
                shareRst = Await ShareMultiAsync(fileIds.ToList, ShareExpirationDate.OneDay, "ptyo")
            End If
            Dim match = Regex.Match(shareRst.Message, """errno"":(\d+).*?""link"":""(.*?)""", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
            If Not match.Success Then
                Return (LegalOptions.Unknow, pathFullpath & "获取分享结果失败", String.Empty)
            End If

            Dim errno = match.Groups(1).Value.ToIntegerOfCulture
            Dim desc = GetShareErrorNoDescription(errno)
            If 0 <> errno Then
                Return (LegalOptions.No, pathFullpath & "分享失败：" & desc, String.Empty)
            End If

            ' 检测是否合法（不可访问就是分享失败）
            ' 不可访问直接展开检测
            Dim shareLink = match.Groups(2).Value.TryUnescape
            Return Await VerifyShareFileAsync(shareLink)
        End Function

        ''' <summary>
        ''' 检验分享文件的有效性。
        ''' </summary>
        ''' <param name="url"></param>
        ''' <returns>文件合法返回True,文件（包含）违规返回False</returns>
        Public Overloads Async Function VerifyShareFileAsync(ByVal url As String) As Task(Of (Legal As LegalOptions, Path As String, Fs_Id As String))
            Try
                Dim rst = Await TryDoGetAsync(url)

                If Not rst.Success Then Return (LegalOptions.Unknow, url, String.Empty)

                Dim notFound = rst.Message.Contains("share_nofound")
                If notFound Then
                    Return (LegalOptions.No, url, String.Empty)
                End If

                ' 登录和未登录时页面能看到的特征字符
                Dim containLegalChars = rst.Message.Contains("请输入提取码") OrElse rst.Message.Contains("失效时间") OrElse rst.Message.Contains("过期时间")
                If Not containLegalChars Then
                    Return (LegalOptions.No, url, String.Empty)
                End If

                ' 获取目录
                Dim shareData = rst.Message.GetFirstMatchValue("locals.mset\((.*?)\)")

                Dim root = MSJsSerializer.Deserialize(Of ShareFileEntity.Root)(shareData)
                For Each f In root.file_list
                    Try
                        ' 文件在进入文件夹的已经浏览了，不需要再浏览一次
                        If 1 <> f.isdir Then Continue For

                        Dim browseRst = Await BrowseDirectoryAsync(f.path, root.uk, root.shareid)
                        If Not browseRst.Success Then
                            Dim path = browseRst.Path
                            Dim match = Regex.Match(path, "sharelink\d+\-(\d+)+(/.*)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
                            If match.Success Then
                                Return (LegalOptions.No, match.Groups(2).Value, match.Groups(1).Value)
                            Else
                                Return (LegalOptions.No, "获取失败", String.Empty)
                            End If
                        End If
                    Catch ex As Exception
                        Logger.WriteLine(ex)
                    End Try
                Next
            Catch ex As TaskCanceledException
                ' do nothing
            End Try

            Return (LegalOptions.Yes, String.Empty, String.Empty)
        End Function

        ''' <summary>
        ''' 浏览分享文件夹
        ''' </summary>
        ''' <param name="dir"></param>
        ''' <param name="uk"></param>
        ''' <param name="shareId"></param>
        ''' <returns></returns>
        Private Async Function BrowseDirectoryAsync(ByVal dir As String, ByVal uk As Integer, ByVal shareId As Long) As Task(Of (Success As Boolean, Path As String))
            RaiseEvent Browsing(Nothing, New BrowsingEventArgs(dir))

            Dim getRst = Await GetShareDirInfoAsync(dir, uk, shareId)
            If Not getRst.Success Then Return (False, dir)
            Dim root = MSJsSerializer.Deserialize(Of ShareDirectoryEntity.Root)(getRst.Message)

            If root.errno <> 0 Then
                Return (False, dir)
            End If

            For Each l In root.list
                If 1 = l.isdir Then
                    Dim rst = Await BrowseDirectoryAsync(l.path, uk, shareId)
                    If Not rst.Success Then
                        Return rst
                    End If
                End If
            Next

            Return (True, String.Empty)
        End Function

        ' base code from :https://referencesource.microsoft.com/#mscorlib/system/array.cs,c9d30a83673759f0
        ' public static int BinarySearch(Array array, int index, int length, Object value, IComparer comparer)
        ''' <summary>
        ''' 采用二分法检测
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        Private Async Function BinaryCheckAsync(ByVal path As List(Of PathInfo)) As Task(Of (ContainIllegalFile As LegalOptions, Path As List(Of PathInfo)))
            If path Is Nothing Then
                Throw New ArgumentNullException(NameOf(path))
            End If
            If path.Count = 0 Then
                Return (LegalOptions.Unknow, New List(Of PathInfo))
            End If

            ' 只有一个文件而且是文件夹则直接展开目录
            If path.Count = 1 AndAlso path(0).IsDir Then
                path = Await GetPathsIdAsync(path(0).FullPath)
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

                If LegalOptions.No = contain.Legal Then
                    If pathSlice.Count = 1 Then
                        If pathSlice(0).IsDir Then
                            ' 进入目录
                            Dim subDirPathId = Await GetPathsIdAsync(pathSlice(0).FullPath)
                            Return Await BinaryCheckAsync(subDirPathId)
                        Else
                            Return (LegalOptions.No, pathSlice)
                        End If
                    End If

                    hi = i
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

        Private Async Function GetPathsIdAsync(ByVal directoryFullPath As String) As Task(Of List(Of PathInfo))
            Dim dir = If(directoryFullPath.Chars(0) = "/"c,
            directoryFullPath.Replace("\", "/"),
            "/" & directoryFullPath.Replace("\", "/"))

            ' 根据路径获取文件夹的 fs Id
            Dim getRst = Await GetDirInfoAsync(dir)
            If Not getRst.Success Then
                RaiseEvent CheckReport(Nothing, New CheckReportEventArgs(LegalOptions.Unknow, "展开文件夹目录失败"))
                Return New List(Of PathInfo)
            End If

            Dim root = MSJsSerializer.Deserialize(Of DirectoryEntity.Root)(getRst.Message)
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
            Dim url = $"https://pan.baidu.com/share/list?uk={uk}&shareid={shareId}&order=other&desc=1&showempty=0&web=1&page=1&num=100&dir={dir}&t=0.42717085533817023&channel=chunlei&web=1&app_id=250528&bdstoken=null&logid={GetBase64LogId()}&clienttype=0"

            Return Await TryDoGetAsync(url)
        End Function
    End Class
End Namespace