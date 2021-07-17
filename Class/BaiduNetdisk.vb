Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Imports ShanXingTech
Imports ShanXingTech.Text2

Namespace ShanXingTech.Net2
    ''' <summary>
    ''' 百度网盘类
    ''' </summary>
    Public Class BaiduNetdisk

#Region "枚举区"

#End Region

#Region "字段区"
        Private ReadOnly m_Random As Random
#End Region

#Region "属性区"
        ' http请求需要的cookies，只要在第一次使用的时候设置就可以了，类内部会自动管理cookies
        Private ReadOnly m_Cookies As CookieContainer
        Private ReadOnly m_BdsToken As String
        Private ReadOnly m_BdUSS As String

        ''' <summary>
        ''' 整个上传操作完成标记
        ''' </summary>
        ''' <returns></returns>
        Private Property isUploadFileCompleted As Boolean

        Private m_UploadFileResult As HttpResponse
        ''' <summary>
        ''' 上传文件结果
        ''' </summary>
        ''' <returns>上传文件结果,IsUploadFileCompleted 表示是否上传完毕，Message 包含上传结果信息</returns>
        Public ReadOnly Property UploadFileResult() As HttpResponse
            Get
                Return m_UploadFileResult
            End Get
        End Property
        ''' <summary>
        ''' 实例是否初始化成功，初始化成功之后才可以进行各种操作
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property IsInit() As Boolean

        Private m_DefaultHttpGetRequestHeaders As Dictionary(Of String, String)
        ''' <summary>
        ''' 向百度网盘发送Get请求需要的请求头
        ''' </summary>
        ''' <returns></returns>
        Public ReadOnly Property DefaultHttpGetRequestHeader() As Dictionary(Of String, String)
            Get
                If m_DefaultHttpGetRequestHeaders Is Nothing Then
                    m_DefaultHttpGetRequestHeaders = New Dictionary(Of String, String) From {
                        {"User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0"},
                        {"Connection", "keep-alive"},
                        {"Accept-Encoding", "gzip, deflate"},
                        {"Referer", "https://pan.baidu.com/disk/home"}
                    }
                End If

                Return m_DefaultHttpGetRequestHeaders
            End Get
        End Property

        Private m_MD5sOfExistFileInUploadPath As Concurrent.ConcurrentDictionary(Of String， List(Of String))
        ''' <summary>
        ''' 网盘上某个uploadPath里已经存在的文件md5列表
        ''' </summary>
        ''' <returns>key 为文件夹名,value 为文件md5</returns>
        Private ReadOnly Property md5sOfExistFileInUploadPath() As Concurrent.ConcurrentDictionary(Of String， List(Of String))
            Get
                If m_MD5sOfExistFileInUploadPath Is Nothing Then
                    m_MD5sOfExistFileInUploadPath = New Concurrent.ConcurrentDictionary(Of String， List(Of String))
                End If

                Return m_MD5sOfExistFileInUploadPath
            End Get
        End Property


#End Region

#Region "事件区"
        Public Event UploadProgressChanged(sender As Object, e As Net.UploadProgressChangedEventArgs)
        'Public Event UploadFileCompleted(sender As Object)
        'Public Event UploadFileCompleted(sender As ValueTuple(Of ValueTuple(Of String, String), String))
        Public Event UploadFileCompleted(fileFullPath As String, uploadPath As String, message As String)
        Private Event OnUploadFileCompleted(sender As Object, e As Net.UploadFileCompletedEventArgs)
#End Region

#Region "构造函数区"
        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="cookies">登录网盘之后的cookies</param>
        ''' <param name="bdsToken"></param>
        ''' <param name="bdUSS"></param>
        Sub New(ByRef cookies As CookieContainer, ByVal bdsToken As String, ByVal bdUSS As String)
            m_Random = New Random

            Me.m_Cookies = cookies
            ' 把传入的cookie装盒并且设置到请求头
            ' cookie只能是这样设置.而且s_HttpClientHandler会内部会自动管理cookies
            ' 不需要多次设置
            Net2.HttpAsync.Instance.ReInit(cookies)

            ' 只有在构造函数内能修改 ReadOnly 字段
            Me.m_BdsToken = bdsToken
            Me.m_BdUSS = bdUSS

            isUploadFileCompleted = False
            m_UploadFileResult = New HttpResponse(False, 0, "Waiting for upload")

            IsInit = True
        End Sub
#End Region

#Region "函数区"
        ''' <summary>
        ''' 执行Post请求
        ''' </summary>
        ''' <param name="url"></param>
        ''' <param name="postData"></param>
        ''' <returns></returns>
        Private Async Function TryDoPostAsync(ByVal url As String, ByVal postData As String) As Task(Of HttpResponse)
            'Dim defaultHttpGetRequestHeaders As New Dictionary(Of String, String) From {
            '    {"User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0"},
            '    {"Connection", "keep-alive"},
            '    {"Referer", "https://pan.baidu.com/disk/home"}
            '}
            Dim httpHeadersParam As New Dictionary(Of String, String) From {
            {"User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0"},
            {"Connection", "keep-alive"}
        }

            ' 处理请求参数
            'Dim keyValueParams As New Dictionary(Of String, String)
            'Dim kvsQueryString = Net.WebUtility.ParseQueryString(postData)
            'For Each key In kvsQueryString.AllKeys
            '    keyValueParams.Add(key, kvsQueryString(key))
            'Next

            Return Await Net2.HttpAsync.Instance.TryPostAsync(url, httpHeadersParam, postData, 3)
        End Function

        Public Async Function TryDoGetAsync(ByVal url As String) As Task(Of HttpResponse)
            Return Await Net2.HttpAsync.Instance.TryGetAsync(url, DefaultHttpGetRequestHeader, 3)
        End Function

        ''' <summary>
        ''' 获取登录信息
        ''' </summary>
        ''' <param name="loginedHtml"></param>
        ''' <returns></returns>
        Public Shared Function GetLoginInfo(ByVal loginedHtml As String) As (BdsToken As String, BdUSS As String)
            'm_BdsToken = "885bc0c371ec21241da3a5f63248724b"
            'm_BdUSS = "pansec_DCb740ccc5511e5e8fedcff06b081203-1BXEtz42RhOdPXw8thmfUr%2BEJBfcDkLhb3RioiBJqY6OgmLJTpbglX5A9Tm%2BiE45vqGoUucEwmXcM%2FUs%2FP1Us8p2eRJLx70Sw7S3eUdDG3ueCrERKsjiDGucUnarSUeEXpuUm29hTMjSkvEn9lSSrv9heoyH0zdxtY%2BYM6Dqm8qjFiBDEfjCVOiy8YN5uv6RfoE9VfhbDL98pCYx%2BJ5%2FQNPXTD2i7GqVsoTFVhKXBI8S%2B9%2F3phuA%2BCbWaZo%2Bcj2E1BpyPDKQN29i%2B3exBXLwzQ%3D%3D"

            ' 访问网盘首页 以获取bdsToken 和 m_BdUSS
            'Dim getHomeRst = Await Net2.GetAsync("https://pan.baidu.com/disk/home", defaultHttpGetRequestHeaders)
            Dim pattern = """bdstoken"":""(\w+)"".*?""XDUSS"":""(.*?)"""
            Dim match = Regex.Match(loginedHtml, pattern, RegexOptions.IgnoreCase Or RegexOptions.Compiled)
            Dim bdsToken = match.Groups(1).Value
            Dim bdUSS = match.Groups(2).Value

            Return (bdsToken, bdUSS)
        End Function

#Region "文件上传"

        ''' <summary>
        ''' 上传文件 第一步
        ''' </summary>
        ''' <param name="url"></param>
        ''' <param name="postData"></param>
        ''' <returns></returns>
        Private Async Function PreCreateFileAsync(ByVal url As String, ByVal postData As String) As Task(Of HttpResponse)
            Return Await TryDoPostAsync(url, postData)
        End Function

        ''' <summary>
        ''' 上传文件 第三步
        ''' </summary>
        ''' <param name="url"></param>
        ''' <param name="postData"></param>
        ''' <returns></returns>
        Private Async Function CreateFileAsync(ByVal url As String, ByVal postData As String) As Task(Of HttpResponse)
            Return Await TryDoPostAsync(url, postData)
        End Function

        ''' <summary>
        ''' 上传文件 第二步
        ''' </summary>
        ''' <param name="uploadInfo"></param>
        Private Async Function UploadFileAsync(ByVal uploadInfo As UploadInfo) As Task
            ' uploadID BDUSS 都不能为空或者有误
            Dim url = $"https://c3.pcs.baidu.com/rest/2.0/pcs/superfile2?method=upload&app_id=250528&channel=chunlei&clienttype=0&uploadClient=1&BDUSS={m_BdUSS}&logid={GetBase64LogId()}&path={uploadInfo.UploadPath}{uploadInfo.FileName}&uploadid={uploadInfo.UploadID}&partseq=0"

            Using uploadClient As New WebClient
                ' 设置请求头
                uploadClient.Headers.Set(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:55.0) Gecko/20100101 Firefox/55.0")
                ' 如果cookie跟bdstoken对不上，可能会导致传输到 50%的时候发生 您的主机中的软件中止了一个已建立的连接 异常
                uploadClient.Headers.Set(HttpRequestHeader.Cookie, m_Cookies.ToKeyValuePairs)

                ' 注册相关事件，以向外部报告上传状态
                AddHandler uploadClient.UploadProgressChanged, AddressOf UploadProgressChangedEventHandle
                AddHandler uploadClient.UploadFileCompleted, AddressOf UploadFileCompletedEventHandle

                ' 异步方式上传文件
                uploadClient.UploadFileAsync(New Uri(url), HttpMethod.POST.ToString, uploadInfo.FileFullPath, uploadInfo)
            End Using

            ' 此处只是获取第三步操作的结果
            While Not isUploadFileCompleted
                Await Task.Delay(500)
            End While
        End Function

        Private Sub UploadProgressChangedEventHandle(sender As Object, e As Net.UploadProgressChangedEventArgs)
            Dim webClient = DirectCast(sender, WebClient)
            RemoveHandler webClient.UploadProgressChanged, AddressOf UploadProgressChangedEventHandle

            RaiseEvent UploadProgressChanged(sender， e)
        End Sub

        Private Sub UploadFileCompletedEventHandle(sender As Object, e As Net.UploadFileCompletedEventArgs)
            Dim webClient = DirectCast(sender, WebClient)
            RemoveHandler webClient.UploadFileCompleted, AddressOf UploadFileCompletedEventHandle

            RaiseEvent OnUploadFileCompleted(sender， e)
        End Sub

        Private Async Sub Me_OnUploadFileCompleted(sender As Object, e As Net.UploadFileCompletedEventArgs) Handles Me.OnUploadFileCompleted
            Dim uploadInfo = DirectCast(e.UserState, UploadInfo)

            ' 如果cookie跟bdstoken对不上，可能会导致传输到 50%的时候发生 您的主机中的软件中止了一个已建立的连接 异常
            ' 另外需要注意！！！
            ' 如果传输到 50%的时候中断，或者刚开始传输就中断，99%几率是cookie没有设置正确，一定要仔细检查
            Dim uploadFileState As String
            ' 处理取消以及异常情况
            If e.Cancelled Then
                uploadFileState = "Work cancel！"

                Debug.Print(Logger.MakeDebugString(String.Concat(uploadFileState)))

                isUploadFileCompleted = True
                m_UploadFileResult = New HttpResponse(False, 0, "Upload cancel")
                RaiseEvent UploadFileCompleted(uploadInfo.FileFullPath, uploadInfo.UploadPath, uploadFileState)

                Return
            ElseIf e.Error IsNot Nothing Then
                Logger.WriteLine(e.Error)

                uploadFileState = $"Error!({e.Error.Message}"

                isUploadFileCompleted = True
                m_UploadFileResult = New HttpResponse(False, 0, "Upload break")
                RaiseEvent UploadFileCompleted(uploadInfo.FileFullPath, uploadInfo.UploadPath, uploadFileState)

                Return
            End If

            isUploadFileCompleted = False
            m_UploadFileResult = New HttpResponse(False, 0, "Waiting for upload")

            Dim uploadRst = System.Text.Encoding.UTF8.GetString(e.Result)
            'Debug.Print(Logger.MakeDebugString( $"上传完毕,等待创建 {uploadRst}"))

            ' 上传文件第三步
            Dim url = $"https://pan.baidu.com/api/create?isdir=0&rtype=1&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&clienttype=0"
            'path =/ 我的资源 / 1.png
            'Size = 31412
            'uploadid = N1-MTE2LjcuMjEuNTM6MTUwMzY1OTkzODo1NDkyMTcyODExNDg2NzU4Nzgy  可以为空啥
            ' 上面的信息等同于 7-116.7.21.53:1503659938:5492172811486758782 格式是：N1-IP:10个字符时间戳:request_id 通过base编码之后就是上面的信息了
            'block_list =["23cefb757d8829c2b0948d127dc3829b"] 不可缺
            ' size 必须要跟实际文件大小一致
            Dim postData = $"path={uploadInfo.UploadPath}{uploadInfo.FileName}&size={uploadInfo.FileSize}&uploadid={uploadInfo.UploadID}&block_list=%5B%22{uploadInfo.FileMd5}%22%5D"
            'Dim postData = ""
            ' {"fs_id":658393148386247,"fileMd5":"23cefb757d8829c2b0948d127dc3829b","server_filename":"02.png","category":3,"path":"\/\u6211\u7684\u8d44\u6e90\/02.png","size":31412,"ctime":1503671183,"mtime":1503671183,"isdir":0,"errno":0,"name":"\/\u6211\u7684\u8d44\u6e90\/02.png"}
            Dim json As HttpResponse = Nothing
            Try
                json = Await CreateFileAsync(url, postData)
                'Debug.Print(Logger.MakeDebugString( json))
                ' 设置上传完成标记 以便异步操作 UploadFileAsync 可以进行获取上传结果操作
                isUploadFileCompleted = True
                m_UploadFileResult = json
            Catch ex As Exception
                Logger.WriteLine(ex)

                ' 设置上传完成标记 以便异步操作 UploadFileAsync 可以进行获取上传结果操作
                isUploadFileCompleted = False
                m_UploadFileResult = New HttpResponse(False, 0, ex.Message)
            End Try

            RaiseEvent UploadFileCompleted(uploadInfo.FileFullPath, uploadInfo.UploadPath, json.Message)
        End Sub

        ''' <summary>
        ''' 从json中获取多个文件的md5值
        ''' </summary>
        ''' <param name="json"></param>
        ''' <returns></returns>
        Private Function GetMD5s(ByVal json As String) As List(Of String)
            Dim pattern = "md5"":""([a-z0-9]+)"""

            If Not Regex.IsMatch(json, pattern, RegexOptions.IgnoreCase Or RegexOptions.Compiled) Then
                Return New List(Of String)
            End If

            Dim matches = Regex.Matches(json, pattern, RegexOptions.IgnoreCase Or RegexOptions.Compiled)
            Dim md5sList As New List(Of String)(30)
            For Each match As Match In matches
                md5sList.Add(match.Groups(1).Value)
            Next

            Return md5sList
        End Function

        ''' <summary>
        ''' 上传本地文件到网盘，不支持远程文件 目前只支持串行上传
        ''' </summary>
        ''' <param name="uploadPath">网盘上的某个文件夹路径；如果是不存在的文件夹，会自动创建</param>
        ''' <param name="fileFullPath">文件全路径</param>
        ''' <param name="uploadMode">上传模式=</param>
        ''' <returns></returns>
        Public Async Function UploadFileAsync(ByVal uploadPath As String, ByVal fileFullPath As String, ByVal uploadMode As UploadMode) As Task(Of HttpResponse)
#Region "防呆检查"
            ' 指定的路径或文件名太长，或者两者都太长。完全限定文件名必须少于 260 个字符，并且目录名必须少于 248 个字符
            ' 所以此处要做检测，文件名长度不能大于248
            If fileFullPath.Length > 248 Then
                Dim uploadFileState = "指定的路径或文件名太长，或者两者都太长。完全限定文件名必须少于 260 个字符，并且目录名必须少于 248 个字符"
                RaiseEvent UploadFileCompleted(fileFullPath, uploadPath, uploadFileState)
                Return New HttpResponse(False, HttpStatusCode.RequestUriTooLong, uploadFileState)
            End If

            ' 检查文件是否存在
            If Not System.IO.File.Exists(fileFullPath) Then
                Dim uploadFileState = "File Not Found"
                RaiseEvent UploadFileCompleted(fileFullPath, uploadPath, uploadFileState)
                Return New HttpResponse(False, HttpStatusCode.NotFound, uploadFileState)
            End If
#End Region

#Region "获取或者设置上传文件需要的信息"
            Dim fileName = String.Empty
            Dim fileSize As Long
            Dim fileMd5 = String.Empty
            Try
                fileName = System.IO.Path.GetFileName(fileFullPath)
                fileSize = My.Computer.FileSystem.GetFileInfo(fileFullPath).Length
                fileMd5 = IO2.File.GetMD5Value(fileFullPath)
            Catch ex As Exception
                RaiseEvent UploadFileCompleted(fileFullPath, uploadPath, ex.Message)
                Return New HttpResponse(False, HttpStatusCode.BadRequest, ex.Message)
            End Try

            ' 处理 uploadPath 使之符合文件夹格式
            If Not uploadPath.EndsWith("/") Then
                uploadPath &= "/"
            End If
            ' 是用的是网络资源的 /而不是本地文件资源的\
            If uploadPath.IndexOf("\") > -1 Then
                uploadPath = uploadPath.Replace("\", "/")
            End If
#End Region


#Region "文件重复性检查01"
            Dim md5sList As New List(Of String)
            ' 如果之前没有获取过这个上传目录的信息，那就需要先获取一遍以得到改目录根目录下所有文件的md5
            Dim containKey = md5sOfExistFileInUploadPath.ContainsKey(uploadPath)
            If Not containKey Then
                Dim getDirInfoRst = Await GetDirInfoAsync(uploadPath)
                If getDirInfoRst.Success Then
                    md5sList = GetMD5s(getDirInfoRst.Message)
                    md5sOfExistFileInUploadPath.TryAdd(uploadPath, md5sList)
                Else
                    Dim uploadFileState = "Can't get uploadpath info"
                    RaiseEvent UploadFileCompleted(fileFullPath, uploadPath, uploadFileState)
                    Return New HttpResponse(False, 0, uploadFileState)
                End If
            End If
            Debug.Print(Logger.MakeDebugString($"is need to get dir ifno = {(Not containKey).ToString}"))

            ' 通过md5值对比列表，如果文件已经存在，并且
            ' 1.上传模式是 UploadMode.Create 则不上传，直接返回 md5信息
            ' 2.上传模式是 UploadMode.RenameIncrementallyOrCreate 则继续上传，待上传操作操作完成会返回上传结果
            If uploadMode = UploadMode.Create Then
                md5sList = md5sOfExistFileInUploadPath(uploadPath)
                If Not md5sList.Count = 0 Then
                    If md5sList.AsParallel.Contains(fileMd5) Then
                        Debug.Print(Logger.MakeDebugString($"{fileName} file duplicate"))

                        isUploadFileCompleted = True
                        m_UploadFileResult = New HttpResponse(True, 0, "File duplicate!")

                        Return m_UploadFileResult
                    End If
                End If

                Debug.Print(Logger.MakeDebugString($"{fileName} file not duplicate"))
            Else
                Debug.Print(Logger.MakeDebugString($"{fileName} {uploadMode.ToString}"))
            End If
#End Region

            isUploadFileCompleted = False
            m_UploadFileResult = New HttpResponse(False, 0, "Waiting for upload")

#Region "上传文件第一步"
            Dim url = $"https://pan.baidu.com/api/precreate?channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&clienttype=0"

            Dim postData = $"path={uploadPath}{fileName}&autoinit=1&block_list=%5B%22{fileMd5}%22%5D"

            Dim json As HttpResponse
            Dim uploadID As String = String.Empty
            Try
                json = Await PreCreateFileAsync(url, postData)
                'Debug.Print(Logger.MakeDebugString( json))

                Dim match = Regex.Match(json.Message, "path"":""(.*?)"",""uploadid"":""([\w-]+=*)""", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
                uploadID = match.Groups(2).Value
            Catch ex As Exception
                Logger.WriteLine(ex)

                ' 设置上传完成标记 以便异步操作 UploadFileAsync 可以进行获取上传结果操作
                isUploadFileCompleted = False
                m_UploadFileResult = New HttpResponse(False, 0, ex.Message)
            End Try
#End Region

#Region "上传文件第二步"
            ' 上传文件第二步
            ' 把上传文件的具体信息传过去，因为第三步需要用
            Dim uploadInfo As New UploadInfo With {
            .UploadID = uploadID,
            .UploadPath = uploadPath,
            .FileFullPath = fileFullPath,
            .FileName = fileName,
            .FileMd5 = fileMd5,
            .FileSize = fileSize
        }
            Await UploadFileAsync(uploadInfo)
#End Region

#Region "上传文件第三步在 Me.OnUploadFileCompleted 事件完成"
#End Region

#Region "文件重复性检查02"
            ' 上传成功之后，如果是 UploadMode.Create模式，说明是新文件，并且已经上传成功了（HttpStatusCode.OK），
            ' 需要把md5信息添加到相应的 uploadPath key中
            ' 需要注意，前面的过程，如果没有上传成功的话，HttpStatusCode 一定不能设置为 HttpStatusCode.OK
            If uploadMode = UploadMode.Create AndAlso m_UploadFileResult.StatusCode = HttpStatusCode.OK Then
                md5sList.Add(fileMd5)
                ' md5sList是引用类型，所以不需要再次添加进去
                'MD5sOfExistFileInUploadPath.TryAdd(uploadPath, md5sList)
            End If
#End Region

            Return m_UploadFileResult
        End Function

        ''' <summary>
        ''' 上传本地文件到网盘，不支持远程文件 目前只支持串行上传。默认上传模式：通过MD5比较，如果文件存在，则上传之后会自动采用 “增量重命名” 方法重命名，格式为 “原文件名(数字)” ；如果文件不存在，则上传
        ''' </summary>
        ''' <param name="uploadPath">网盘上的某个文件夹路径；如果是不存在的文件夹，会自动创建</param>
        ''' <param name="fileFullPath">文件全路径</param>
        ''' <returns></returns>
        Public Async Function UploadFileAsync(ByVal uploadPath As String, ByVal fileFullPath As String) As Task(Of HttpResponse)
            Return Await UploadFileAsync(uploadPath, fileFullPath, UploadMode.RenameIncrementallyOrCreate)
        End Function
#End Region

        Private Sub OpenFileInternal(needsHeaderAndBoundary As Boolean, fileName As String, ByRef fs As FileStream, ByRef buffer As Byte(), ByRef formHeaderBytes As Byte(), ByRef boundaryBytes As Byte())
            ' 核心代码来自 System.Net.WebClient
            fileName = Path.GetFullPath(fileName)

            fs = New FileStream(fileName, FileMode.Open, FileAccess.Read)
            Dim num As Integer = 8192
            Dim webReq As HttpWebRequest = DirectCast(WebRequest.Create(""), HttpWebRequest)

            If needsHeaderAndBoundary Then
                Dim text2 As String = "---------------------" & Date.Now.Ticks.ToString("x", Globalization.NumberFormatInfo.InvariantInfo)
                webReq.ContentType = "multipart/form-data; boundary=" & text2
                Dim s As String = $"--{text2}{vbCrLf}Content-Disposition: Form-Data; name=""file""; filename=""{Path.GetFileName(fileName)}""""{vbCrLf}Content-Type: application/octet-stream{vbCrLf}{vbCrLf}"
                formHeaderBytes = Encoding.UTF8.GetBytes(s)
                boundaryBytes = Encoding.ASCII.GetBytes(vbCrLf & "--" & text2 & "--" & vbCrLf)
            Else
                'formHeaderBytes = New Byte(-1) {}
                'boundaryBytes = New Byte(-1) {}
                formHeaderBytes = Array.Empty(Of Byte)()
                boundaryBytes = Array.Empty(Of Byte)()
            End If

            If fs.CanSeek Then
                num = CInt(Math.Min(8192L, fs.Length))
            End If
            buffer = New Byte(num - 1) {}

            Dim requestStream = webReq.GetRequestStream()
            requestStream.Write(formHeaderBytes, 0, formHeaderBytes.Length)

            requestStream.Write(boundaryBytes, 0, boundaryBytes.Length)
            requestStream.Close()
        End Sub

        'Private Async Function DownloadFileWithWebApiAsync(ByVal url As String, ByVal fileSaveFullName As String) As Task(Of HttpResponse)
        '    '########### 此函数未实现 ###########

        '    ' errno 112 表示页面已过期
        '    'Dim downloadLink = Await GetFileDownloadLinkAsync(url)
        '    'Return Await UploadFileAsync()
        'End Function

        'Private Async Function DownloadFileWithAppApiAsync(ByVal url As String, ByVal fileSaveFullName As String) As Task(Of HttpResponse)
        '    '########### 此函数未实现 ###########

        '    ' errno 112 表示页面已过期
        '    'Dim downloadLink = Await GetFileDownloadLinkAsync(url)
        '    'Return Await UploadFileAsync()
        'End Function

        ''' <summary>
        ''' 获取百度网盘文件的下载链接
        ''' </summary>
        ''' <param name="url"></param>
        ''' <returns></returns>
        Private Async Function GetFileDownloadLinkAsync(ByVal url As String) As Task(Of String)
            Dim json = Await Net2.HttpAsync.Instance.GetAsync(url)

            Dim pattern = """dlink"":""(.*?)"""
            Dim match = Regex.Match(json.Message, pattern, RegexOptions.IgnoreCase Or RegexOptions.Compiled)
            Dim downloadLink = match.Groups(1).Value

            Return downloadLink
        End Function

        ''' <summary>
        ''' 按照默认条件搜索文件或者文件夹
        ''' </summary>
        ''' <param name="keyword">要搜索的关键词</param>
        ''' <returns>返回第一页，前100个结果。在 Message 里面可以找到 has_more 标记，值为 1 时表示还有更多结果</returns>
        Public Async Function SearchAsync(ByVal keyword As String) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/search?recursion=1&order=time&desc=1&showempty=0&uploadClient=1&page=1&num=100&key={keyword}&t=0.{CStr(Now.ToFileTime)}&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            Return Await Net2.HttpAsync.Instance.GetAsync(url, DefaultHttpGetRequestHeader)
        End Function

        ''' <summary>
        ''' 按照传入条件搜索文件或者文件夹
        ''' </summary>
        ''' <param name="keyword">要搜索的关键词</param>
        ''' <param name="page">返回哪一页的结果</param>
        ''' <param name="pageSize">一页返回多少条结果,建议设置为默认100，设置越大返回的数据量越多（如果满足条件话）</param>
        ''' <returns></returns>
        Public Async Function SearchAsync(ByVal keyword As String， ByVal page As Integer, ByVal pageSize As Integer) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/search?recursion=1&order=time&desc=1&showempty=0&uploadClient=1&page={CStr(page)}&num={CStr(pageSize)}&key={keyword}&t=0.{CStr(Now.ToFileTime)}&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            Return Await Net2.HttpAsync.Instance.GetAsync(url, DefaultHttpGetRequestHeader)
        End Function

        ''' <summary>
        ''' 获取网盘信息。 建议在执行创建、删除等任务前后执行一次以确定网盘容量信息
        ''' </summary>
        ''' <returns>返回 总容量(单位：B 下同)、可用容量、已用容量、是否到期等信息</returns>
        Public Async Function GetNetDiskInfoAsync() As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/quota?checkexpire=1&checkfree=1&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            Return Await TryDoGetAsync(url)
        End Function

        ''' <summary>
        ''' 获取网盘首页目录信息
        ''' </summary>
        ''' <returns>操作执行完成 Success 返回 True ,否则返回 False ;Message 返回操作结果</returns>
        Public Async Function GetHomeDirInfoAsync() As Task(Of HttpResponse)
            Return Await GetDirInfoAsync("/")
        End Function

        ''' <summary>
        ''' 获取网盘某个目录信息
        ''' </summary>
        ''' <param name="dir">目录路径。不需要编码,如 '/软件包' </param>
        ''' <returns></returns>
        Public Async Function GetDirInfoAsync(ByVal dir As String) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/list?dir={dir}&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&num=100&order=time&desc=1&clienttype=0&showempty=0&uploadClient=1&page=1&channel=chunlei&app_id=250528"

            Return Await TryDoGetAsync(url)
        End Function

        ''' <summary>
        ''' 检查某个目录是否存在
        ''' </summary>
        ''' <param name="dir">目录路径。不需要编码</param>
        ''' <returns></returns>
        Public Async Function ExistsDir(ByVal dir As String) As Task(Of Boolean)
            Dim getRst = Await GetDirInfoAsync(dir)
            Dim funcRst = getRst.Success AndAlso getRst.Message.IndexOf("""errno"":0") > -1

            Return funcRst
        End Function

        ''' <summary>
        ''' 检查某个文件是否存在
        ''' </summary>
        ''' <param name="fileFullPath">文件路径。不需要编码</param>
        ''' <returns></returns>
        Public Async Function ExistsFile(ByVal fileFullPath As String) As Task(Of Boolean)
            ' 先获取到文件的目录信息，然后再从目录中查找是否存在文件
            Dim filePath = IO.Path.GetDirectoryName(fileFullPath)
            filePath = filePath.Replace("\", "/")
            Dim getRst = Await GetDirInfoAsync(filePath)
            If getRst.Success AndAlso getRst.Message.IndexOf("""errno"":0") > -1 Then
                ' 只要返回的信息中含有 file 的文件名，就说明此目录包含 file 文件
                Dim fileName = IO.Path.GetFileName(fileFullPath)

                Return getRst.Message.IndexOf(fileName) > -1
            Else
                Return False
            End If
        End Function

        ''' <summary>
        ''' 创建文件夹
        ''' </summary>
        ''' <param name="dir">欲创建的文件件路径。不需要编码
        ''' <para>如果是子文件夹，需要带上父文件夹的的路径，如 "/我的资源/新建文件夹"；</para>
        ''' <para>如果父文件夹不存在，则会自动创建父文件夹。</para>
        ''' </param>
        ''' <returns>创建成功返回新创建文件夹的信息；如果文件夹已经存在，则自动在 <paramref name="dir"/>的基础上加上 “(递增数字)” 格式后缀作为新的文件夹名</returns>
        Public Async Function CreateDirAsync(ByVal dir As String) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/create?a=commit&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            Dim postData = $"path={dir}&isdir=1&block_list=%5B%5D"

            Return Await TryDoPostAsync(url, postData)
        End Function

        ''' <summary>
        ''' 获取目标（文件、文件夹等）序列化形式字符串
        ''' </summary>
        ''' <param name="targets"></param>
        ''' <returns></returns>
        Private Function GetTargetsSerialString(ByVal targets As String()) As String
            ' 路径只有一个的情况
            If targets.Length = 1 Then
                Dim targetsSerial = $"""{targets(0)}"""
                Dim postData = $"filelist=[{targetsSerial}]"
                Return targetsSerial
            End If

            ' 路径有两个或者两个以上的情况
            Dim sb As New StringBuilder(100)
            For Each d In targets
                sb.Append("""").Append(d).Append("""").Append(",")
            Next
            If sb.Chars(sb.Length - 1) = ","c Then
                sb.Remove(sb.Length - 1, 1)
            End If
            Dim targetsSerial2 = sb.ToString

            Return targetsSerial2
        End Function

        ''' <summary>
        ''' 执行删除操作（删除文件或者文件夹）
        ''' </summary>
        ''' <param name="postData"></param>
        ''' <param name="targetsSerial">可以是单一目标的序列化字符串形式，也可以是多个目标的序列化字符串形式</param>
        ''' <returns></returns>
        Private Async Function DoDeleteAsync(ByVal postData As String, ByVal targetsSerial As String) As Task(Of HttpResponse)
            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/api/filemanager?opera=delete&async=2&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            ' 提交删除任务
            Dim json = Await TryDoPostAsync(url, postData)

            Return Await QueryTaskAsync(json.Message, targetsSerial)
        End Function

        ''' <summary>
        ''' 删除多个文件夹
        ''' </summary>
        ''' <param name="dirs">文件夹路径数组，文件夹格式如：“/第一层目录名/第二层目录名”</param>
        ''' <returns>如果成功，Message 不返回任何信息，并且Success置为True;如果失败，则 Message 返回具体错误信息</returns>
        Public Async Function DeleteDirAsync(ByVal dirs As String()) As Task(Of HttpResponse)
            ' 没有设置路径的情况
            If dirs Is Nothing OrElse dirs.Length = 0 Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "dirs error")
            End If

            Dim targetsSerial = GetTargetsSerialString(dirs)
            Dim postData = $"filelist=[{targetsSerial}]"

            Return Await DoOperateAndReturnResult(postData, targetsSerial)
        End Function

        ''' <summary>
        ''' 删除单个文件夹
        ''' </summary>
        ''' <param name="dir">文件夹路径,格式如：“/第一层目录名/第二层目录名”</param>
        ''' <returns>如果成功，Message 不返回任何信息，并且Success置为True;如果失败，则 Message 返回具体错误信息</returns>
        Public Async Function DeleteDirAsync(ByVal dir As String) As Task(Of HttpResponse)
            ' 没有设置路径的情况
            If String.IsNullOrEmpty(dir) Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "dir error")
            End If

            Dim dirSerial = $"""{dir}"""
            Dim postData = $"filelist=[{dirSerial}]"
            Return Await DoOperateAndReturnResult(postData, dirSerial)
        End Function

        ''' <summary>
        ''' 删除多个文件
        ''' </summary>
        ''' <param name="filePaths">文件路径数组，文件格式如：“/第一层目录名/第二层目录名/文件名.后缀”</param>
        ''' <returns>如果成功，Message 不返回任何信息，并且Success置为True;如果失败，则 Message 返回具体错误信息</returns>
        Public Async Function DeleteFileAsync(ByVal filePaths As String()) As Task(Of HttpResponse)
            ' 没有设置路径的情况
            If filePaths Is Nothing OrElse filePaths.Length = 0 Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "filePaths error")
            End If

            Dim targetsSerial = GetTargetsSerialString(filePaths)
            Dim postData = $"filelist=[{targetsSerial}]"

            Return Await DoOperateAndReturnResult(postData, targetsSerial)
        End Function

        ''' <summary>
        ''' 删除单个文件
        ''' </summary>
        ''' <param name="filePath">文件路径，文件格式如：“/第一层目录名/第二层目录名/文件名.后缀”</param>
        ''' <returns>如果成功，Message 不返回任何信息，并且Success置为True;如果失败，则 Message 返回具体错误信息</returns>
        Public Async Function DeleteFileAsync(ByVal filePath As String) As Task(Of HttpResponse)
            ' 没有设置路径的情况
            If String.IsNullOrEmpty(filePath) Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "filePath error")
            End If

            Dim filePathSerial = $"""{filePath}"""
            Dim postData = $"filelist=[{filePathSerial}]"

            Return Await DoOperateAndReturnResult(postData, filePathSerial)
        End Function

        ''' <summary>
        ''' 执行操作并且返回操作结果
        ''' </summary>
        ''' <param name="postData">请求文本</param>
        ''' <param name="targetsSerial">需操作的对象</param>
        ''' <returns>如果成功，Message 不返回任何信息，并且Success置为True;如果失败，则 Message 返回具体错误信息</returns>
        Private Async Function DoOperateAndReturnResult(ByVal postData As String, ByVal targetsSerial As String) As Task(Of HttpResponse)
            Dim optRst = Await DoDeleteAsync(postData, targetsSerial)
            ' 如果成功，不需要返回任何信息，并且Success置为True;如果失败，则返回具体错误信息
            Return If(optRst.Success AndAlso optRst.Message.IndexOf("""status"":""success""") > -1,
                 New HttpResponse(True, HttpStatusCode.OK, String.Empty),
                 New HttpResponse(False, optRst.StatusCode, optRst.Message))
        End Function

        ''' <summary>
        ''' 查询任务执行结果
        ''' </summary>
        ''' <param name="json"></param>
        ''' <param name="targetsSerial"></param>
        ''' <returns></returns>
        Private Async Function QueryTaskAsync(ByVal json As String, ByVal targetsSerial As String) As Task(Of HttpResponse)
            ' 查询删除任务执行结果
            Dim taskID = GetTaskID(json)
            If taskID.Length = 0 Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "can't get taskID")
            End If

            ' 没有设置路径的情况
            If String.IsNullOrEmpty(targetsSerial) Then
                Return New HttpResponse(False, HttpStatusCode.BadRequest, "targetsSerial error")
            End If

            Dim postData = $"filelist=[{targetsSerial}]"

            ' logid 可有可无
            Dim url = $"https://pan.baidu.com/share/taskquery?taskid={taskID}&channel=chunlei&uploadClient=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}&clienttype=0"

            ' 延时1.5秒再查下任务执行结果
            Await Task.Delay(1500)
            Dim postRst = Await TryDoPostAsync(url, postData)

            ' 如果返回等待中的结果的话再次执行
            ' {"errno":0,"request_id":5635977565314194634,"task_errno":0,"status":"pending"})
            While postRst.Message.IndexOf("status"":""pending") > -1
                postRst = Await TryDoPostAsync(url, postData)
            End While

            Return postRst
        End Function

        ''' <summary>
        ''' 获取任务ID
        ''' </summary>
        ''' <param name="json"></param>
        ''' <returns></returns>
        Private Function GetTaskID(ByVal json As String) As String
            ' {"errno":0,"info":[],"request_id":5613293733633063781,"taskid":313646291191125}
            Dim pattern = "taskid"":(\d+)}"
            Dim match = Regex.Match(json, pattern, RegexOptions.IgnoreCase Or RegexOptions.Compiled)

            Return match.Groups(1).Value
        End Function

        ''' <summary>
        ''' 生成操作百度网盘需要的参数LogID
        ''' </summary>
        ''' <returns></returns>
        Public Shared Function GetBase64LogId() As String
            Dim funcRst As String = $"{Date.Now.ToTimeStampString(TimePrecision.Millisecond)}0.{CStr(Now.ToFileTime)}"
            funcRst = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(funcRst))

            Return funcRst
        End Function

        ''' <summary>
        ''' 分享文件（文件夹）。使用 <see cref="GetShareErrorNoDescription(Integer)"/> 获取 error 对应的描述。
        ''' </summary>
        ''' <param name="fileId"></param>
        ''' <param name="expirationDate">有效期</param>
        ''' <param name="privatePassword">4个字符长度提取码,默认随机创建</param>
        ''' <returns></returns>
        Public Async Function ShareAsync(ByVal fileId As String, ByVal expirationDate As ShareExpirationDate, Optional ByVal privatePassword As String = Nothing) As Task(Of HttpResponse)
            Return Await InternalShareAsync(fileId, expirationDate, privatePassword)
        End Function

        ''' <summary>
        ''' 分享多个文件（文件夹）。使用 <see cref="GetShareErrorNoDescription(Integer)"/> 获取 error 对应的描述。
        ''' </summary>
        ''' <param name="fileIds"></param>
        ''' <param name="expirationDate">有效期</param>
        ''' <param name="privatePassword">4个字符长度提取码,默认随机创建</param>
        ''' <returns></returns>
        Public Async Function ShareMultiAsync(ByVal fileIds As List(Of String), ByVal expirationDate As ShareExpirationDate, Optional ByVal privatePassword As String = Nothing) As Task(Of HttpResponse)
            Dim temp = String.Join(",", fileIds)
            Return Await InternalShareAsync(temp, expirationDate, privatePassword)
        End Function

        ''' <summary>
        '''  分享文件（文件夹）。使用 <see cref="GetShareErrorNoDescription(Integer)"/> 获取 error 对应的描述。
        ''' </summary>
        ''' <param name="fileIdList"></param>
        ''' <param name="expirationDate">有效期</param>
        ''' <param name="privatePassword">4个字符长度提取码,默认随机创建</param>
        ''' <returns></returns>
        Private Async Function InternalShareAsync(ByVal fileIdList As String, ByVal expirationDate As ShareExpirationDate, Optional ByVal privatePassword As String = Nothing) As Task(Of HttpResponse)
            Dim url = $"https://pan.baidu.com/share/set?channel=chunlei&clienttype=0&web=1&channel=chunlei&web=1&app_id=250528&bdstoken={m_BdsToken}&logid={GetBase64LogId()}=&clienttype=0"
            Dim postData = $"schannel=4&channel_list=[]&period={expirationDate.ToStringOfCulture}&pwd={If(privatePassword, MakePrivatePassword())}&fid_list=[{fileIdList}]"
            Return Await TryDoPostAsync(url, postData)
        End Function

        ''' <summary>
        ''' 获取分享操作返回状态码对应的描述
        ''' <para>错误信息可以参考： https://nd-static.bdstatic.com/v20-static/static/home/js/home.862649c2.js </para>
        ''' </summary>
        ''' <param name="errorNo"></param>
        ''' <returns></returns>
        Public Shared Function GetShareErrorNoDescription(ByVal errorNo As Integer) As String
            Dim dic As New Dictionary(Of Integer, String) From {
                {0, "成功"},
                {-1, "由于您分享了违反相关法律法规的文件，分享功能已被禁用，之前分享出去的文件不受影响。"},
                {-2, "用户不存在,请刷新页面后重试"},
                {-3, "文件不存在,请刷新页面后重试"},
                {-4, "登录信息有误，请重新登录试试"},
                {-5, "登录信息有误，请重新登录试试"},
                {-6, "请重新登录"},
                {-7, "该分享已删除或已取消"},
                {-8, "该分享已经过期"},
                {-9, "访问密码错误"},
                {-10, "分享外链已经达到最大上限100000条，不能再次分享"},
                {-11, "验证cookie无效"},
                {-14, "对不起，短信分享每天限制20条，你今天已经分享完，请明天再来分享吧！"},
                {-15, "对不起，邮件分享每天限制20封，你今天已经分享完，请明天再来分享吧！"},
                {-16, "对不起，该文件已经限制分享！"},
                {-17, "文件分享超过限制"},
                {-19, "验证码输入错误，请重试"},
                {-20, "请求验证码失败，请重试"},
                {-21, "未绑定手机或邮箱，没有权限私密分享"},
                {-22, "被分享的文件无法重命名，移动等操作"},
                {-30, "文件已存在"},
                {-31, "文件保存失败"},
                {-32, "你的空间不足了哟，赶紧购买空间吧"},
                {-33, "一次支持操作999个，减点试试吧"},
                {-40, "热门推荐失败"},
                {-60, "相关推荐数据异常"},
                {-62, "密码输入次数达到上限"},
                {-64, "描述包含敏感词"},
                {-70, "你分享的文件中包含病毒或疑似病毒，为了你和他人的数据安全，换个文件分享吧"},
                {1, "服务器错误"},
                {2, "参数错误"},
                {3, "未登录或帐号无效"},
                {4, "存储好像出问题了，请稍候再试"},
                {12, "批量处理错误"},
                {14, "网络错误，请稍候重试"},
                {15, "操作失败，请稍候重试"},
                {16, "网络错误，请稍候重试"},
                {105, "创建链接失败，请重试"},
                {106, "文件读取失败，请<a href=""javascript:window.location.reload();"">刷新</a>页面后重试'"},
                {108, "文件名有敏感词，优化一下吧"},
                {110, "分享次数超出限制，可以到“"我的分享"”中查看已分享的文件链接"},
                {112, "页面已过期，请<a style=""color: #06A7FF;"" href=""javascript:window.location.reload();"">刷新</a>后重试"},
                {113, "外链签名有误"},
                {114, "当前任务不存在，保存失败"},
                {115, "该文件禁止分享"},
                {2126, "文件名中含有敏感词"},
                {2161, "文件中含有违规内容"},
                {2162, "对方加你为好友之后才能发送"},
                {2135, "对方拒绝接收消息"},
                {2136, "对方拒接非好友消息"},
                {2102, "群组不存在"},
                {2103, "你已退出该群"},
                {2101, "你已达到创建2000群上限"},
                {2100, "用户都已经被添加过"},
                {2119, "群成员已满"},
                {9100, "你的帐号存在违规行为，已被冻结，<a href=""/disk/appeal"" target=""_blank"">查看详情</a>"},
                {9200, "你的帐号存在违规行为，已被冻结，<a href=""/disk/appeal"" target=""_blank"">查看详情</a>"},
                {9300, "你的帐号存在违规行为，该功能暂被冻结，<a href=""/disk/appeal"" target=""_blank"">查看详情</a>"},
                {9400, "你的帐号异常，需验证后才能使用该功能，<a href=""/disk/appeal"" target=""_blank"">立即验证</a>"},
                {9500, "你的帐号存在安全风险，已进入保护模式，请修改密码后使用，<a href=""/disk/appeal"" target=""_blank"">查看详情</a>"}
            }

            Return dic(errorNo)
        End Function

        ''' <summary>
        ''' 生成提取码
        ''' </summary>
        ''' <returns></returns>
        Private Function MakePrivatePassword() As String
            ' 只要生成随机四位密码就行，不需要用官网的Js提取码生成算法
            Dim e = "123456789abcdefghijkmnpqrstuvwxyz".ToCharArray
            Dim sb = StringBuilderCache.Acquire(360)
            For i = 1 To 4
                sb.Append(e(m_Random.Next(0, e.Length)))
            Next
            Return StringBuilderCache.GetStringAndReleaseBuilder(sb)
        End Function
#End Region
    End Class
End Namespace