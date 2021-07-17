<Serializable>
Public Class ShareResultCacheInfo
    Public Sub New(fs_Ids As String)
        Me.Fs_Ids = fs_Ids
    End Sub

    Public Property ErrorNo As Integer
    Public Property Link As String
    Public Property ErrorDescription As String
    ''' <summary>
    ''' 分享时间
    ''' </summary>
    ''' <returns></returns>
    Public Property Time As Date
    ''' <summary>
    ''' 分享过期时间
    ''' </summary>
    ''' <returns></returns>
    Public Property ExpirationTime As Date
    Public Property Paths As String
    Public Property Fs_Ids As String

End Class
