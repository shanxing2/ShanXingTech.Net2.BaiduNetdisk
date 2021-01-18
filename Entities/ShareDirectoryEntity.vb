Namespace ShanXingTech.Net2
	Partial Class BdVerifier
		Public Class ShareDirectoryEntity
			Public Class List
				''' <summary>
				''' 
				''' </summary>
				Public Property isdir() As Integer
				''' <summary>
				''' /sharelink655338460-378533402193109/问题解决/注册MSVM.EXE失败
				''' </summary>
				Public Property path() As String
				''' <summary>
				''' 注册MSVM.EXE失败
				''' </summary>
				Public Property server_filename() As String
			End Class

			Public Class Root
				''' <summary>
				''' 
				''' </summary>
				Public Property errno() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property list() As List(Of List)
			End Class
		End Class
	End Class

End Namespace

