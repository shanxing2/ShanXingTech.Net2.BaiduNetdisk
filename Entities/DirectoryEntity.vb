
Namespace ShanXingTech.Net2
	Partial Class BaiduNetdisk
		Public Class DirectoryEntity
			Public Class List
				''' <summary>
				''' 
				''' </summary>
				Public Property server_mtime() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property privacy() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property category() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property unlist() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property isdir() As Integer
				''' <summary>
				''' 文件夹里面没有文件夹 = 1
				''' </summary>
				Public Property dir_empty() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property oper_id() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property server_ctime() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property local_mtime() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property size() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property share() As Integer
				''' <summary>
				''' 软件包
				''' </summary>
				Public Property server_filename() As String
				''' <summary>
				''' /软件包
				''' </summary>
				Public Property path() As String
				''' <summary>
				''' 
				''' </summary>
				Public Property local_ctime() As Long
				''' <summary>
				''' 此属性有误，请勿使用
				''' </summary>
				Public Property empty() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property fs_id() As Long
			End Class

			Public Class Root
				''' <summary>
				''' 
				''' </summary>
				Public Property errno() As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property guid_info() As String
				''' <summary>
				''' 
				''' </summary>
				Public Property list() As List(Of List)
				''' <summary>
				''' 
				''' </summary>
				Public Property request_id() As Long
				''' <summary>
				''' 
				''' </summary>
				Public Property guid() As Integer
			End Class
		End Class
	End Class
End Namespace
