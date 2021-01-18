Namespace ShanXingTech.Net2

    Partial Class BdVerifier
        Public Class BrowsingEventArgs
            Inherits EventArgs

            Public Sub New(status As String)
                Me.Status = status
            End Sub

            Public Property Status As String
        End Class
    End Class
End Namespace
