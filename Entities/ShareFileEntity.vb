Namespace ShanXingTech.Net2
	Partial Class BdVerifier
		Public Class ShareFileEntity
			Public Class List
				''' <summary>
				''' 
				''' </summary>
				Public Property isdir() As Integer
					Get
						Return m_isdir
					End Get
					Set
						m_isdir = Value
					End Set
				End Property
				Private m_isdir As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property parent_path() As String
					Get
						Return m_parent_path
					End Get
					Set
						m_parent_path = Value
					End Set
				End Property
				Private m_parent_path As String
				''' <summary>
				''' /sharelink655338460-378533402193109/问题解决
				''' </summary>
				Public Property path() As String
					Get
						Return m_path
					End Get
					Set
						m_path = Value
					End Set
				End Property
				Private m_path As String
				''' <summary>
				''' 
				''' </summary>
				Public Property status() As String
					Get
						Return m_status
					End Get
					Set
						m_status = Value
					End Set
				End Property
				Private m_status As String
				''' <summary>
				''' 
				''' </summary>
				Public Property server_filename() As String
					Get
						Return m_server_filename
					End Get
					Set
						m_server_filename = Value
					End Set
				End Property
				Private m_server_filename As String
				''' <summary>
				''' 
				''' </summary>
				Public Property fs_id() As String
					Get
						Return m_fs_id
					End Get
					Set
						m_fs_id = Value
					End Set
				End Property
				Private m_fs_id As String
			End Class

			Public Class File_list
				''' <summary>
				''' 
				''' </summary>
				Public Property errno() As Integer
					Get
						Return m_errno
					End Get
					Set
						m_errno = Value
					End Set
				End Property
				Private m_errno As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property list() As List(Of List)
					Get
						Return m_list
					End Get
					Set
						m_list = Value
					End Set
				End Property
				Private m_list As List(Of List)
			End Class

			Public Class Root
				''' <summary>
				''' 
				''' </summary>
				Public Property file_list() As File_list
					Get
						Return m_file_list
					End Get
					Set
						m_file_list = Value
					End Set
				End Property
				Private m_file_list As File_list
				''' <summary>
				''' 
				''' </summary>
				Public Property uk() As Integer
					Get
						Return m_uk
					End Get
					Set
						m_uk = Value
					End Set
				End Property
				Private m_uk As Integer
				''' <summary>
				''' 
				''' </summary>
				Public Property shorturl() As String
					Get
						Return m_shorturl
					End Get
					Set
						m_shorturl = Value
					End Set
				End Property
				Private m_shorturl As String

				''' <summary>
				''' 
				''' </summary>
				Public Property shareid() As Long
					Get
						Return m_shareid
					End Get
					Set
						m_shareid = Value
					End Set
				End Property
				Private m_shareid As Long
			End Class
		End Class

	End Class


End Namespace