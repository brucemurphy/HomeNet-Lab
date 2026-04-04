Public Class ServiceDialog
    Public Property ServiceName As String
    Public Property ServicePort As Integer
    Public Property ServiceEnabled As Boolean

    Private Sub ServiceDialog_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' Load existing values if editing
        txtServiceName.Text = ServiceName
        txtServicePort.Text = If(ServicePort > 0, ServicePort.ToString(), "")
        chkServiceEnabled.IsChecked = ServiceEnabled

        txtServiceName.Focus()
    End Sub

    Private Sub OkButton_Click(sender As Object, e As RoutedEventArgs)
        ' Validate inputs
        If String.IsNullOrWhiteSpace(txtServiceName.Text) Then
            MessageBox.Show("Please enter a service name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            txtServiceName.Focus()
            Return
        End If

        Dim port As Integer
        If Not Integer.TryParse(txtServicePort.Text, port) OrElse port < 1 OrElse port > 65535 Then
            MessageBox.Show("Please enter a valid port number (1-65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning)
            txtServicePort.Focus()
            Return
        End If

        ' Set properties
        ServiceName = txtServiceName.Text.Trim()
        ServicePort = port
        ServiceEnabled = chkServiceEnabled.IsChecked = True

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
