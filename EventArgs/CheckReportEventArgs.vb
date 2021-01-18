Namespace ShanXingTech.Net2
    Partial Class BdVerifier
        Public Class CheckReportEventArgs
            Inherits CheckBaseEventArgs

            Public Sub New(legal As LegalOptions, message As String)
                MyBase.New(legal, message)
            End Sub
        End Class
    End Class
End Namespace

