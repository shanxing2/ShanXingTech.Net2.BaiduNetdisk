<Serializable>
Public Class BdVerifierConf
    ''' <summary>
    ''' 百度网盘登录后的cookie
    ''' </summary>
    ''' <returns></returns>
    Public Property BdCookies As Net.CookieContainer
    ''' <summary>
    ''' 从登录百度网盘后的网页中获取到的 bdstoken
    ''' </summary>
    ''' <returns></returns>
    Public Property BdsToken As String
    ''' <summary>
    ''' 从登录百度网盘后的网页中获取到的 XDUSS
    ''' </summary>
    ''' <returns></returns>
    Public Property BdUSS As String
    ''' <summary>
    ''' 分享文件的自定义提取码
    ''' </summary>
    ''' <returns></returns>
    Public Property SharePrivatePassword As String
    ''' <summary>
    ''' 检测文件的自定义提取码
    ''' </summary>
    ''' <returns></returns>
    Public Property VerifyPrivatePassword As String
    ''' <summary>
    ''' 分享文件的自定义有效期
    ''' </summary>
    ''' <returns></returns>
    Public Property ShareExpirationDate As ShanXingTech.Net2.BaiduNetdisk.ShareExpirationDate
    ''' <summary>
    ''' 检测文件的自定义有效期
    ''' </summary>
    ''' <returns></returns>
    Public Property VerifyExpirationDate As ShanXingTech.Net2.BaiduNetdisk.ShareExpirationDate
    ''' <summary>
    ''' 存储一个或多个文件的fs_Id拼接之后计算出来的md5值（键）和对应的分享链接（值）
    ''' </summary>
    ''' <returns></returns>
    Public Property Fs_Ids_Md5_ShareLinkDic As Dictionary(Of String, ShareResultCacheInfo)
End Class
