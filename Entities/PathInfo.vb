Namespace ShanXingTech.Net2
    Partial Class BdVerifier
        Public Class PathInfo
            Public Sub New()
            End Sub

            Public Sub New(id As Long, isDir As Boolean)
                Me.Id = id
                Me.IsDir = isDir
            End Sub

            Public Sub New(id As Long, isDir As Boolean, fullPath As String)
                Me.Id = id
                Me.IsDir = isDir
                Me.FullPath = fullPath
            End Sub

            Public Property Id As Long
            Public Property FullPath As String
            Public Property IsDir As Boolean

        End Class
    End Class

End Namespace