Imports System.ComponentModel

Namespace ShanXingTech.Net2
    Partial Class BaiduNetdisk
        Public Enum ShareExpirationDate
            ''' <summary>
            ''' 永久有效
            ''' </summary>
            <Description("永久有效")>
            Forever = 0
            ''' <summary>
            ''' 一天有效
            ''' </summary>
            <Description("一天有效")>
            OneDay = 1
            ''' <summary>
            ''' 七天有效
            ''' </summary>
            <Description("七天有效")>
            SevenDay = 7
        End Enum
    End Class
End Namespace