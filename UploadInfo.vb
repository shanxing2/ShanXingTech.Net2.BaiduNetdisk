Namespace ShanXingTech.Net2
    Partial Class BaiduNetdisk
        ''' <summary>
        ''' 上传文件相关信息类
        ''' </summary>
        Private Class UploadInfo
            ''' <summary>
            ''' 上传ID
            ''' </summary>
            ''' <returns></returns>
            Public Property UploadID As String
            ''' <summary>
            ''' 上传路径（网盘某个路径）
            ''' </summary>
            ''' <returns></returns>
            Public Property UploadPath As String
            ''' <summary>
            ''' 文件绝对路径
            ''' </summary>
            ''' <returns></returns>
            Public Property FileFullPath As String
            ''' <summary>
            ''' 文件名,包含后缀
            ''' </summary>
            ''' <returns></returns>
            Public Property FileName As String
            ''' <summary>
            ''' 文件MD5
            ''' </summary>
            ''' <returns></returns>
            Public Property FileMd5 As String
            ''' <summary>
            ''' 文件大小
            ''' </summary>
            ''' <returns></returns>
            Public Property FileSize As Long
        End Class
    End Class

End Namespace
