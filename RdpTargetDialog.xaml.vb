Public Class RdpTargetDialog
    Public Property TargetName As String
    Public Property TargetPort As Integer
    Public Property TargetFileName As String
    Public Property TargetEnabled As Boolean

    Private Sub RdpTargetDialog_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' Load existing values if editing
        txtTargetName.Text = TargetName
        txtTargetPort.Text = If(TargetPort > 0, TargetPort.ToString(), "")
        txtTargetFileName.Text = TargetFileName
        chkTargetEnabled.IsChecked = TargetEnabled

        txtTargetName.Focus()
    End Sub

    Private Sub OkButton_Click(sender As Object, e As RoutedEventArgs)
        ' Validate inputs
        If String.IsNullOrWhiteSpace(txtTargetName.Text) Then
            MessageBox.Show("Please enter a target name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            txtTargetName.Focus()
            Return
        End If

        Dim port As Integer
        If Not Integer.TryParse(txtTargetPort.Text, port) OrElse port < 1 OrElse port > 65535 Then
            MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            txtTargetPort.Focus()
            Return
        End If

        If String.IsNullOrWhiteSpace(txtTargetFileName.Text) Then
            MessageBox.Show("Please enter an RDP filename.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            txtTargetFileName.Focus()
            Return
        End If

        ' Ensure filename ends with .rdp
        Dim fileName = txtTargetFileName.Text.Trim()
        If Not fileName.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase) Then
            fileName &= ".rdp"
        End If

        ' Set properties
        TargetName = txtTargetName.Text.Trim()
        TargetPort = port
        TargetFileName = fileName
        TargetEnabled = chkTargetEnabled.IsChecked = True

        ' Close dialog with OK result
        Me.DialogResult = True
        Me.Close()
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As RoutedEventArgs)
        Me.DialogResult = False
        Me.Close()
    End Sub

    Private Sub Window_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If e.Key = Key.Escape Then
            CancelButton_Click(Nothing, Nothing)
        End If
    End Sub
End Class
