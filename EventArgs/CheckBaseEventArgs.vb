Namespace ShanXingTech.Net2
    Partial Class BdVerifier
        Public Class CheckBaseEventArgs
            Inherits EventArgs

            Public Sub New(legal As LegalOptions, message As String)
                Me.Legal = legal
                Me.Message = message
            End Sub

            ''' <summary>
            ''' 文件合法性
            ''' </summary>
            ''' <returns></returns>
            Public Property Legal As LegalOptions
            ''' <summary>
            ''' 检测结果说明
            ''' </summary>
            ''' <returns></returns>
            Public Property Message As String

        End Class
    End Class

End Namespace
