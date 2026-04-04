Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.ComponentModel
Imports System.Windows.Interop
Imports System.Windows.Media.Effects
Imports System.Windows.Threading
Imports System.Xml
Imports System.Collections.ObjectModel

Class MainWindow
    Private Shared ReadOnly httpClient As New HttpClient()
    Private Const BaseTitle As String = "HomeNet Lab"

    ' Configuration
    Private config As XmlDocument
    Private loadedConfigFile As String = ""
    Private externalIP As String = ""
    Private lastFtpError As String = ""
    Private isManualPublish As Boolean = False
    Private notifyIcon As System.Windows.Forms.NotifyIcon

    ' Activity indicator
    Private _activityBlinkTimer As DispatcherTimer
    Private _activityActive As Boolean = False
    Private _uploadInProgress As Boolean = False
    Private ReadOnly _rand As New Random()

    ' Service and RDP target editors
    Private servicesCollection As New ObservableCollection(Of ServiceItem)
    Private rdpTargetsCollection As New ObservableCollection(Of RdpTargetItem)

    ' Bing wallpaper refresh
    Private _wallpaperRefreshTimer As DispatcherTimer
    Private _lastWallpaperUpdate As DateTime = DateTime.MinValue

    Private Const DwmwaUseImmersiveDarkMode As Integer = 20

    <DllImport("dwmapi.dll", PreserveSig:=True)>
    Private Shared Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
    End Function

    Private Async Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Me.Title = BaseTitle
        EnableDarkTitleBar()
        InitActivityIndicator()
        InitializeSystemTray()
        LoadConfiguration()
        Await UpdateExternalIPAsync()
        UpdateIPDisplay()
        UpdateConfigDisplay()
        Await SetBingWallpaperAsync()
        StartInternetConnectionMonitor()
        StartAutoPublishTimer()
        StartWallpaperRefreshTimer()

        ' Perform immediate publish on startup
        isManualPublish = False ' Silent publish, no popups
        Await PublishWebPortalAsync()

        ' Start minimized to system tray
        Me.WindowState = WindowState.Minimized
        Me.Hide()
        notifyIcon.Visible = True
    End Sub

    Private Sub MainWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        ' Clean up system tray icon
        If notifyIcon IsNot Nothing Then
            notifyIcon.Visible = False
            notifyIcon.Dispose()
        End If
    End Sub

    Private Sub MainWindow_StateChanged(sender As Object, e As EventArgs) Handles Me.StateChanged
        If Me.WindowState = WindowState.Minimized Then
            Me.Hide()
            notifyIcon.Visible = True
            notifyIcon.ShowBalloonTip(1000, "HomeNet Lab", "Application minimized to system tray", System.Windows.Forms.ToolTipIcon.Info)
        End If
    End Sub

    Private Sub InitializeSystemTray()
        notifyIcon = New System.Windows.Forms.NotifyIcon()

        ' Load the application icon - try embedded resource first, then file, then fallback
        Try
            ' Try to load from embedded resource
            Dim iconStream = Application.GetResourceStream(New Uri("pack://application:,,,/HomeNetLab.ico"))
            If iconStream IsNot Nothing Then
                notifyIcon.Icon = New System.Drawing.Icon(iconStream.Stream)
            Else
                ' Try to load from file
                Dim iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HomeNetLab.ico")
                If File.Exists(iconPath) Then
                    notifyIcon.Icon = New System.Drawing.Icon(iconPath)
                Else
                    ' Fallback to embedded icon from application executable
                    notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location)
                End If
            End If
        Catch
            ' If all else fails, use system icon
            notifyIcon.Icon = System.Drawing.SystemIcons.Application
        End Try

        notifyIcon.Text = "HomeNet Lab"
        notifyIcon.Visible = False

        ' Double-click to restore
        AddHandler notifyIcon.DoubleClick, Sub(s, e)
                                               Me.Show()
                                               Me.WindowState = WindowState.Normal
                                               notifyIcon.Visible = False
                                           End Sub

        ' Context menu
        Dim contextMenu = New System.Windows.Forms.ContextMenuStrip()

        Dim publishItem = contextMenu.Items.Add("Publish Web Portal")
        AddHandler publishItem.Click, Async Sub(s, e)
                                          isManualPublish = True
                                          Await UpdateExternalIPAsync()
                                          UpdateIPDisplay()
                                          Await PublishWebPortalAsync()
                                          If isManualPublish Then
                                              ShowPublishResult()
                                          End If
                                      End Sub

        contextMenu.Items.Add("-")

        Dim showItem = contextMenu.Items.Add("Show Window")
        AddHandler showItem.Click, Sub(s, e)
                                       Me.Show()
                                       Me.WindowState = WindowState.Normal
                                       notifyIcon.Visible = False
                                   End Sub

        contextMenu.Items.Add("-")

        Dim exitItem = contextMenu.Items.Add("Exit")
        AddHandler exitItem.Click, Sub(s, e)
                                       Application.Current.Shutdown()
                                   End Sub

        notifyIcon.ContextMenuStrip = contextMenu
    End Sub

    Private Sub ExitMenuItem_Click(sender As Object, e As RoutedEventArgs)
        Application.Current.Shutdown()
    End Sub

    Private Sub EditConfigMenuItem_Click(sender As Object, e As RoutedEventArgs)
        ' Hide config display, show config editor
        ConfigDisplay.Visibility = Visibility.Collapsed
        ConfigEditor.Visibility = Visibility.Visible

        ' Load current config values into editor
        LoadConfigIntoEditor()
    End Sub

    Private Sub SaveConfigButton_Click(sender As Object, e As RoutedEventArgs)
        ' Save config and refresh
        If SaveConfigFromEditor() Then
            ' Success - reload config and update display
            LoadConfiguration()
            UpdateConfigDisplay()

            ' Hide editor, show config display
            ConfigEditor.Visibility = Visibility.Collapsed
            ConfigDisplay.Visibility = Visibility.Visible

            MessageBox.Show("Configuration saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information)
        End If
    End Sub

    Private Sub CancelConfigButton_Click(sender As Object, e As RoutedEventArgs)
        ' Just hide editor, show config display without saving
        ConfigEditor.Visibility = Visibility.Collapsed
        ConfigDisplay.Visibility = Visibility.Visible
    End Sub

    Private Sub SetupPortForwardingMenuItem_Click(sender As Object, e As RoutedEventArgs)
        Dim setupDialog As New StringBuilder()
        setupDialog.AppendLine("🔧 Port Forwarding Setup Wizard")
        setupDialog.AppendLine(New String("="c, 50))
        setupDialog.AppendLine()
        setupDialog.AppendLine("For RDP access to work from outside your network, you need:")
        setupDialog.AppendLine()
        setupDialog.AppendLine("1️⃣ ROUTER PORT FORWARDING (External → Host PC)")
        setupDialog.AppendLine("   Configure these rules on your router:")
        setupDialog.AppendLine()

        ' Get RDP targets from config
        Dim rdpTargets As New List(Of (Name As String, Port As Integer))
        Try
            If config IsNot Nothing Then
                Dim targetNodes = config.SelectNodes("//RDPTargets/Target")
                If targetNodes IsNot Nothing AndAlso targetNodes.Count > 0 Then
                    For Each targetNode As XmlNode In targetNodes
                        Dim name = targetNode.SelectSingleNode("Name")?.InnerText
                        Dim portStr = targetNode.SelectSingleNode("Port")?.InnerText
                        Dim enabled = targetNode.SelectSingleNode("Enabled")?.InnerText

                        If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(portStr) Then
                            Dim port As Integer
                            If Integer.TryParse(portStr, port) Then
                                rdpTargets.Add((name, port))
                            End If
                        End If
                    Next
                End If
            End If
        Catch
        End Try

        If rdpTargets.Count = 0 Then
            rdpTargets.Add(("Remote Desktop", 3389))
        End If

        ' Build PowerShell commands for clipboard
        Dim clipboardCommands As New StringBuilder()

        ' Show router rules
        For Each target In rdpTargets
            setupDialog.AppendLine($"   Port {target.Port} (TCP) → {GetLocalIP()}:{target.Port}")
            setupDialog.AppendLine($"   ({target.Name})")
            setupDialog.AppendLine()
        Next

        setupDialog.AppendLine("2️⃣ HOST PC PORT FORWARDING (For VPCs only)")
        setupDialog.AppendLine("   Run these commands AS ADMINISTRATOR on your host PC:")
        setupDialog.AppendLine()

        Dim hasVpcs As Boolean = False
        For Each target In rdpTargets
            If target.Port <> 3389 Then
                hasVpcs = True
                Dim vpcLocalIp = "192.168.x.x"  ' Placeholder
                Dim command = $"netsh interface portproxy add v4tov4 listenport={target.Port} connectaddress={vpcLocalIp} connectport=3389"
                setupDialog.AppendLine($"   {command}")
                setupDialog.AppendLine($"   ({target.Name} - Update {vpcLocalIp} with actual VPC IP)")
                setupDialog.AppendLine()

                ' Add to clipboard commands
                clipboardCommands.AppendLine($"# {target.Name}")
                clipboardCommands.AppendLine($"{command.Replace(vpcLocalIp, "192.168.100." & (target.Port - 3389))}")
                clipboardCommands.AppendLine()
            End If
        Next

        If Not hasVpcs Then
            setupDialog.AppendLine("   ✅ Not needed - Only host PC configured")
            setupDialog.AppendLine()
        Else
            ' Add utility commands to clipboard
            clipboardCommands.AppendLine("# View all current rules")
            clipboardCommands.AppendLine("netsh interface portproxy show all")
            clipboardCommands.AppendLine()
            clipboardCommands.AppendLine("# Delete a rule (example)")
            clipboardCommands.AppendLine("# netsh interface portproxy delete v4tov4 listenport=3390")
        End If

        setupDialog.AppendLine("3️⃣ VIEW CURRENT PORT PROXY RULES")
        setupDialog.AppendLine("   netsh interface portproxy show all")
        setupDialog.AppendLine()
        setupDialog.AppendLine("4️⃣ REMOVE PORT PROXY RULES (if needed)")
        setupDialog.AppendLine("   netsh interface portproxy delete v4tov4 listenport=3390")
        setupDialog.AppendLine()
        setupDialog.AppendLine("📋 Your Configuration:")
        setupDialog.AppendLine($"   Host PC: {GetPCName()}")
        setupDialog.AppendLine($"   Local IP: {GetLocalIP()}")
        setupDialog.AppendLine($"   External IP: {externalIP}")
        setupDialog.AppendLine()

        If hasVpcs Then
            setupDialog.AppendLine("💡 TIP: Click YES to copy PowerShell commands to clipboard")
            setupDialog.AppendLine("   Then paste into PowerShell (run as Administrator)")
            setupDialog.AppendLine()
            setupDialog.AppendLine("Copy commands to clipboard?")
        Else
            setupDialog.AppendLine("No VPCs configured - only host PC RDP enabled.")
            Return
        End If

        Dim result = MessageBox.Show(setupDialog.ToString(), "Port Forwarding Setup", MessageBoxButton.YesNo, MessageBoxImage.Information)

        If result = MessageBoxResult.Yes AndAlso hasVpcs Then
            Try
                Clipboard.SetText(clipboardCommands.ToString())
                MessageBox.Show("✅ Commands copied to clipboard!" & Environment.NewLine & Environment.NewLine &
                               "Next steps:" & Environment.NewLine &
                               "1. Open PowerShell as Administrator" & Environment.NewLine &
                               "2. Paste commands (Ctrl+V)" & Environment.NewLine &
                               "3. Update VPC IP addresses in commands" & Environment.NewLine &
                               "4. Run commands" & Environment.NewLine &
                               "5. Verify with: netsh interface portproxy show all",
                               "Commands Copied", MessageBoxButton.OK, MessageBoxImage.Information)
            Catch ex As Exception
                MessageBox.Show($"Could not copy to clipboard:{Environment.NewLine}{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
            End Try
        End If
    End Sub

    Private Async Sub PublishWebPortalMenuItem_Click(sender As Object, e As RoutedEventArgs)
        isManualPublish = True

        ' Update IP first
        Await UpdateExternalIPAsync()
        UpdateIPDisplay()

        ' Publish portal
        Await PublishWebPortalAsync()

        ' Show result only for manual publish
        If isManualPublish Then
            ShowPublishResult()
        End If
    End Sub

    Private Sub ShowPublishResult()
        Dim success = String.IsNullOrEmpty(lastFtpError)

        If success Then
            MessageBox.Show($"Web portal published successfully!{Environment.NewLine}External IP: {externalIP}{Environment.NewLine}{Environment.NewLine}Files uploaded to:{Environment.NewLine}ftp://{GetConfigValue("//FTP/Server").Replace("ftp://", "")}{GetConfigValue("//FTP/RemotePath")}", "Success", MessageBoxButton.OK, MessageBoxImage.Information)
        Else
            Dim errorMsg As New StringBuilder()
            errorMsg.AppendLine("Failed to publish web portal.")
            errorMsg.AppendLine()
            If Not String.IsNullOrEmpty(lastFtpError) Then
                errorMsg.AppendLine("Error Details:")
                errorMsg.AppendLine(lastFtpError)
                errorMsg.AppendLine()
            End If
            errorMsg.AppendLine("Please check:")
            errorMsg.AppendLine("• FTP server address is correct")
            errorMsg.AppendLine("• Username and password are valid")
            errorMsg.AppendLine("• Remote path exists and has write permissions")
            errorMsg.AppendLine("• Internet connection is active")
            errorMsg.AppendLine("• Firewall isn't blocking FTP")

            MessageBox.Show(errorMsg.ToString(), "Upload Failed", MessageBoxButton.OK, MessageBoxImage.Error)
        End If

        isManualPublish = False
    End Sub

    Private Sub EnableDarkTitleBar()
        Try
            Dim hwnd = New WindowInteropHelper(Me).Handle
            If hwnd <> IntPtr.Zero Then
                Dim useDarkMode As Integer = 1
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, useDarkMode, Marshal.SizeOf(Of Integer)())
            End If
        Catch
        End Try
    End Sub

    Private Sub InitActivityIndicator()
        ' Activity light now represents internet connection status
        ' Setup blinking timer for FTP upload indication
        _activityBlinkTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(300)}
        AddHandler _activityBlinkTimer.Tick, Sub()
                                                 If _uploadInProgress Then
                                                     If _activityActive Then
                                                         UpdateActivityLightDim()
                                                         _activityActive = False
                                                     Else
                                                         UpdateActivityLightActive()
                                                         _activityActive = True
                                                     End If
                                                 End If
                                             End Sub
        UpdateActivityLightOff()
    End Sub

    Private Sub StartActivityIndicator()
        ' Show internet connected (solid green)
        UpdateActivityLightActive()
    End Sub

    Private Sub StopActivityIndicator()
        ' Show internet disconnected
        UpdateActivityLightOff()
    End Sub

    Private Sub StartUploadBlink()
        ' Start blinking for FTP upload
        Dispatcher.Invoke(Sub()
                              _uploadInProgress = True
                              _activityActive = False
                              _activityBlinkTimer?.Start()
                          End Sub)
    End Sub

    Private Sub StopUploadBlink()
        ' Stop blinking, return to connection status
        Dispatcher.Invoke(Sub()
                              _uploadInProgress = False
                              _activityBlinkTimer?.Stop()
                          End Sub)

        ' Return to connection status
        Task.Run(Async Function()
                     Dim isConnected = Await IsInternetAvailableAsync()
                     Await Dispatcher.InvokeAsync(Sub()
                                                      If isConnected Then
                                                          StartActivityIndicator()
                                                      Else
                                                          StopActivityIndicator()
                                                      End If
                                                  End Sub)
                     Return Nothing
                 End Function)
    End Sub

    Private Sub UpdateActivityLightActive()
        Dispatcher.Invoke(Sub()
                              Activity.Fill = New SolidColorBrush(Color.FromRgb(0, 255, 100))
                              Activity.Effect = New DropShadowEffect With {
                .Color = Color.FromRgb(0, 255, 100),
                .BlurRadius = 12,
                .ShadowDepth = 0,
                .Opacity = 0.8
            }
                          End Sub)
    End Sub

    Private Sub UpdateActivityLightOff()
        Dispatcher.Invoke(Sub()
                              Activity.Fill = New SolidColorBrush(Color.FromRgb(50, 50, 50))
                              Activity.Effect = Nothing
                          End Sub)
    End Sub

    Private Sub UpdateActivityLightDim()
        Dispatcher.Invoke(Sub()
                              Activity.Fill = New SolidColorBrush(Color.FromRgb(25, 80, 55))
                              Activity.Effect = Nothing
                          End Sub)
    End Sub

    Private Async Function SetBingWallpaperAsync(Optional ct As CancellationToken = Nothing) As Task
        If Not Await IsInternetAvailableAsync(ct) Then
            SetFallbackBackground()
            Return
        End If
        Const xmlUrl As String = "https://www.bing.com/HPImageArchive.aspx?format=xml&idx=0&n=1&mkt=en-US"
        Dim xmlContent As String
        Try
            Using resp = Await httpClient.GetAsync(xmlUrl, ct)
                resp.EnsureSuccessStatusCode()
                xmlContent = Await resp.Content.ReadAsStringAsync(ct)
            End Using
        Catch
            SetFallbackBackground()
            Return
        End Try

        Dim doc As XDocument
        Try
            doc = XDocument.Parse(xmlContent)
        Catch
            SetFallbackBackground()
            Return
        End Try

        Dim imageElement = doc.Root?.Element("image")
        If imageElement Is Nothing Then
            SetFallbackBackground()
            Return
        End If

        Dim relativeUrl = imageElement.Element("url")?.Value
        If String.IsNullOrWhiteSpace(relativeUrl) Then
            SetFallbackBackground()
            Return
        End If

        Dim fullImageUrl = If(relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase),
                              relativeUrl,
                              "https://www.bing.com" & relativeUrl)

        Dim imageBytes As Byte()
        Try
            imageBytes = Await httpClient.GetByteArrayAsync(fullImageUrl, ct)
        Catch
            SetFallbackBackground()
            Return
        End Try

        Dim bmp As New BitmapImage()
        Try
            Using ms As New MemoryStream(imageBytes)
                bmp.BeginInit()
                bmp.CacheOption = BitmapCacheOption.OnLoad
                bmp.StreamSource = ms
                bmp.EndInit()
                bmp.Freeze()
            End Using
        Catch
            SetFallbackBackground()
            Return
        End Try

        Me.Background = New ImageBrush(bmp) With {.Stretch = Stretch.UniformToFill, .AlignmentX = AlignmentX.Center, .AlignmentY = AlignmentY.Center}

        Dim headline = imageElement.Element("headline")?.Value
        Dim copyright = imageElement.Element("copyright")?.Value

        Await Dispatcher.InvokeAsync(Sub()
                                         HeadingTextBlock.Text = If(String.IsNullOrWhiteSpace(headline), "Description", headline)
                                         CopyrightTextBlock.Text = If(String.IsNullOrWhiteSpace(copyright), "Detail", copyright)
                                     End Sub)
    End Function

    Private Sub SetFallbackBackground()
        Me.Background = New SolidColorBrush(Color.FromRgb(128, 128, 128))
    End Sub

    Private Shared Async Function IsInternetAvailableAsync(Optional ct As CancellationToken = Nothing) As Task(Of Boolean)
        Try
            Using resp = Await httpClient.GetAsync("https://www.bing.com", HttpCompletionOption.ResponseHeadersRead, ct)
                Return resp.IsSuccessStatusCode
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Sub LoadConfigIntoEditor()
        Try
            ' FTP Settings
            EditFtpServer.Text = GetConfigValue("//FTP/Server")
            EditFtpUsername.Text = GetConfigValue("//FTP/Username")
            EditRemotePath.Text = GetConfigValue("//FTP/RemotePath")

            Dim htmlFileName = GetConfigValue("//FTP/HtmlFileName")
            EditHtmlFileName.Text = If(String.IsNullOrEmpty(htmlFileName), "index.html", htmlFileName)

            ' Password needs special handling since it's a PasswordBox
            EditFtpPassword.Password = GetConfigValue("//FTP/Password")

            ' Security Settings
            EditAccessPassword.Password = GetConfigValue("//Security/AccessPassword")

            ' Auto-Publish Settings
            Dim autoPublishEnabled = GetConfigValue("//AutoPublish/Enabled")
            EditAutoPublishEnabled.IsChecked = (autoPublishEnabled.ToLower() = "true")

            Dim frequency = GetConfigValue("//AutoPublish/FrequencySeconds")
            EditAutoPublishFrequency.Text = If(String.IsNullOrEmpty(frequency), "300", frequency)

            ' Load Services
            servicesCollection.Clear()
            Try
                If config IsNot Nothing Then
                    Dim serviceNodes = config.SelectNodes("//Services/Service")
                    If serviceNodes IsNot Nothing Then
                        For Each serviceNode As XmlNode In serviceNodes
                            Dim name = serviceNode.SelectSingleNode("Name")?.InnerText
                            Dim portStr = serviceNode.SelectSingleNode("Port")?.InnerText
                            Dim enabled = serviceNode.SelectSingleNode("Enabled")?.InnerText

                            If Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(portStr) Then
                                Dim port As Integer
                                If Integer.TryParse(portStr, port) Then
                                    servicesCollection.Add(New ServiceItem With {
                                        .Name = name,
                                        .Port = port,
                                        .Enabled = If(enabled?.ToLower() = "true", True, False)
                                    })
                                End If
                            End If
                        Next
                    End If
                End If
            Catch
            End Try

            EditServicesList.ItemsSource = servicesCollection

            ' Load RDP Targets
            rdpTargetsCollection.Clear()
            Try
                If config IsNot Nothing Then
                    Dim targetNodes = config.SelectNodes("//RDPTargets/Target")
                    If targetNodes IsNot Nothing Then
                        For Each targetNode As XmlNode In targetNodes
                            Dim name = targetNode.SelectSingleNode("Name")?.InnerText
                            Dim portStr = targetNode.SelectSingleNode("Port")?.InnerText
                            Dim fileName = targetNode.SelectSingleNode("FileName")?.InnerText
                            Dim enabled = targetNode.SelectSingleNode("Enabled")?.InnerText

                            If Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(portStr) AndAlso Not String.IsNullOrEmpty(fileName) Then
                                Dim port As Integer
                                If Integer.TryParse(portStr, port) Then
                                    rdpTargetsCollection.Add(New RdpTargetItem With {
                                        .Name = name,
                                        .Port = port,
                                        .FileName = fileName,
                                        .Enabled = If(enabled?.ToLower() = "true", True, False)
                                    })
                                End If
                            End If
                        Next
                    End If
                End If
            Catch
            End Try

            EditRdpTargetsList.ItemsSource = rdpTargetsCollection

            ' File info
            Dim pcName = GetPCName()
            If loadedConfigFile = "config.xml" Then
                EditConfigFileInfo.Text = $"Currently using: config.xml{Environment.NewLine}This is the default configuration file."
                EditSaveAsPCName.Visibility = Visibility.Visible
                EditSaveAsPCName.Content = $"Save as {pcName}.xml (PC-specific config)"
                EditSaveAsPCName.IsChecked = False
            Else
                EditConfigFileInfo.Text = $"Currently using: {loadedConfigFile}{Environment.NewLine}This is a PC-specific configuration file."
                EditSaveAsPCName.Visibility = Visibility.Collapsed
            End If

        Catch ex As Exception
            MessageBox.Show($"Error loading configuration:{Environment.NewLine}{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Function SaveConfigFromEditor() As Boolean
        Try
            ' Determine save path
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory
            Dim savePath As String
            Dim saveFileName As String

            ' Check if user wants to save as PC-specific config
            If EditSaveAsPCName.Visibility = Visibility.Visible AndAlso EditSaveAsPCName.IsChecked = True Then
                saveFileName = $"{GetPCName()}.xml"
                savePath = Path.Combine(baseDir, saveFileName)
            ElseIf loadedConfigFile <> "config.xml" AndAlso loadedConfigFile <> "Not found" AndAlso loadedConfigFile <> "Error loading" Then
                ' Already using PC-specific config, save to that file
                saveFileName = loadedConfigFile
                savePath = Path.Combine(baseDir, saveFileName)
            Else
                ' Save to config.xml
                saveFileName = "config.xml"
                savePath = Path.Combine(baseDir, saveFileName)
            End If

            ' Create new XML document
            Dim newConfig As New XmlDocument()

            ' Create XML declaration
            Dim xmlDeclaration = newConfig.CreateXmlDeclaration("1.0", "utf-8", Nothing)
            newConfig.AppendChild(xmlDeclaration)

            ' Create root element
            Dim root = newConfig.CreateElement("Configuration")
            newConfig.AppendChild(root)

            ' FTP Section
            Dim ftpNode = newConfig.CreateElement("FTP")
            AppendConfigElement(newConfig, ftpNode, "Server", EditFtpServer.Text)
            AppendConfigElement(newConfig, ftpNode, "Username", EditFtpUsername.Text)
            AppendConfigElement(newConfig, ftpNode, "Password", EditFtpPassword.Password)
            AppendConfigElement(newConfig, ftpNode, "RemotePath", EditRemotePath.Text)
            AppendConfigElement(newConfig, ftpNode, "HtmlFileName", EditHtmlFileName.Text)
            root.AppendChild(ftpNode)

            ' Security Section
            Dim securityNode = newConfig.CreateElement("Security")
            AppendConfigElement(newConfig, securityNode, "AccessPassword", EditAccessPassword.Password)
            root.AppendChild(securityNode)

            ' RDP Targets Section (from editor)
            Dim rdpTargetsNode = newConfig.CreateElement("RDPTargets")
            For Each targetItem In rdpTargetsCollection
                Dim targetNode = newConfig.CreateElement("Target")
                AppendConfigElement(newConfig, targetNode, "Name", targetItem.Name)
                AppendConfigElement(newConfig, targetNode, "Port", targetItem.Port.ToString())
                AppendConfigElement(newConfig, targetNode, "FileName", targetItem.FileName)
                AppendConfigElement(newConfig, targetNode, "Enabled", If(targetItem.Enabled, "true", "false"))
                rdpTargetsNode.AppendChild(targetNode)
            Next
            root.AppendChild(rdpTargetsNode)

            ' Services Section (from editor)
            Dim servicesNode = newConfig.CreateElement("Services")
            For Each serviceItem In servicesCollection
                Dim serviceNode = newConfig.CreateElement("Service")
                AppendConfigElement(newConfig, serviceNode, "Name", serviceItem.Name)
                AppendConfigElement(newConfig, serviceNode, "Port", serviceItem.Port.ToString())
                AppendConfigElement(newConfig, serviceNode, "Enabled", If(serviceItem.Enabled, "true", "false"))
                servicesNode.AppendChild(serviceNode)
            Next
            root.AppendChild(servicesNode)

            ' Auto-Publish Section
            Dim autoPublishNode = newConfig.CreateElement("AutoPublish")
            AppendConfigElement(newConfig, autoPublishNode, "Enabled", If(EditAutoPublishEnabled.IsChecked = True, "true", "false"))
            AppendConfigElement(newConfig, autoPublishNode, "FrequencySeconds", EditAutoPublishFrequency.Text)
            root.AppendChild(autoPublishNode)

            ' Save to file with proper formatting
            Dim settings As New XmlWriterSettings() With {
                .Indent = True,
                .IndentChars = "  ",
                .NewLineChars = Environment.NewLine,
                .Encoding = Encoding.UTF8
            }

            Using writer As XmlWriter = XmlWriter.Create(savePath, settings)
                newConfig.Save(writer)
            End Using

            ' Update loaded config file name
            loadedConfigFile = saveFileName
            Me.Title = If(saveFileName = "config.xml", BaseTitle, $"{BaseTitle} - {saveFileName}")

            Return True

        Catch ex As Exception
            MessageBox.Show($"Error saving configuration:{Environment.NewLine}{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
            Return False
        End Try
    End Function

    Private Sub AppendConfigElement(doc As XmlDocument, parent As XmlElement, elementName As String, value As String)
        Dim element = doc.CreateElement(elementName)
        element.InnerText = value
        parent.AppendChild(element)
    End Sub

    Private Sub LoadConfiguration()
        Try
            config = New XmlDocument()
            Dim baseDir = AppDomain.CurrentDomain.BaseDirectory

            ' Try to load PC-specific config file first (e.g., MURPHYSRV.xml)
            Dim pcName = GetPCName()
            Dim pcConfigPath = Path.Combine(baseDir, $"{pcName}.xml")
            Dim configPath = Path.Combine(baseDir, "config.xml")

            If File.Exists(pcConfigPath) Then
                ' Load PC-specific config
                config.Load(pcConfigPath)
                loadedConfigFile = $"{pcName}.xml"
                Me.Title = $"{BaseTitle} - {loadedConfigFile}"
            ElseIf File.Exists(configPath) Then
                ' Fall back to default config.xml
                config.Load(configPath)
                loadedConfigFile = "config.xml"
                Me.Title = BaseTitle
            Else
                ' No config file found
                loadedConfigFile = "Not found"
                Me.Title = $"{BaseTitle} - No config"
            End If
        Catch ex As Exception
            ' Config file optional, will use defaults or skip FTP upload
            loadedConfigFile = "Error loading"
            Me.Title = $"{BaseTitle} - Config error"
        End Try
    End Sub

    Private Function GetConfigValue(xpath As String) As String
        Try
            If config Is Nothing Then Return ""
            Dim node = config.SelectSingleNode(xpath)
            Return If(node IsNot Nothing, node.InnerText, "")
        Catch
            Return ""
        End Try
    End Function

    Private Async Function UpdateExternalIPAsync() As Task
        Try
            externalIP = Await GetExternalIPAsync()
        Catch ex As Exception
            externalIP = "Unable to retrieve"
        End Try
    End Function

    Private Function GetLocalIP() As String
        Try
            Dim host = Dns.GetHostEntry(Dns.GetHostName())
            For Each ip In host.AddressList
                If ip.AddressFamily = Net.Sockets.AddressFamily.InterNetwork Then
                    Return ip.ToString()
                End If
            Next
            Return "Unable to retrieve"
        Catch
            Return "Unable to retrieve"
        End Try
    End Function

    Private Function GetPCName() As String
        Try
            Return Environment.MachineName
        Catch
            Return "UNKNOWN"
        End Try
    End Function

    Private Function GetFormattedLocalIP() As String
        Try
            Dim pcName = GetPCName()
            Dim localIP = GetLocalIP()
            Return $"{pcName} | {localIP}"
        Catch
            Return "Unable to retrieve"
        End Try
    End Function

    Private Sub UpdateIPDisplay()
        Try
            Dispatcher.Invoke(Sub()
                                  MountSizeProgressText.Text = externalIP
                                  MountSizeProgressTextDetail.Text = GetFormattedLocalIP()
                              End Sub)
        Catch
            ' Ignore errors during IP display update
        End Try
    End Sub

    Private Sub ShowPublishingStatus(stepText As String, detailText As String)
        Try
            Dispatcher.BeginInvoke(Sub()
                                       MountSizeProgressText.Text = stepText
                                       MountSizeProgressTextDetail.Text = detailText
                                   End Sub)
        Catch
        End Try
    End Sub

    Private Sub UpdateConfigDisplay()
        Try
            Dispatcher.Invoke(Sub()
                                  ' Config File Name
                                  ConfigFileName.Text = loadedConfigFile

                                  ' FTP Configuration
                                  ConfigFtpServer.Text = GetConfigValue("//FTP/Server").Replace("ftp://", "")
                                  ConfigFtpUsername.Text = GetConfigValue("//FTP/Username")

                                  ' Remote Path with filename
                                  Dim remotePath = GetConfigValue("//FTP/RemotePath")
                                  Dim htmlFileName = GetConfigValue("//FTP/HtmlFileName")
                                  If String.IsNullOrEmpty(htmlFileName) Then htmlFileName = "index.html"
                                  ConfigRemotePath.Text = $"{remotePath}{htmlFileName}"

                                  ' Service Configuration - Get all enabled services
                                  Dim serviceNames As New List(Of String)
                                  Dim servicePorts As New List(Of String)

                                  Try
                                      If config IsNot Nothing Then
                                          Dim serviceNodes = config.SelectNodes("//Services/Service")
                                          If serviceNodes IsNot Nothing Then
                                              For Each serviceNode As XmlNode In serviceNodes
                                                  Dim name = serviceNode.SelectSingleNode("Name")?.InnerText
                                                  Dim port = serviceNode.SelectSingleNode("Port")?.InnerText
                                                  Dim enabled = serviceNode.SelectSingleNode("Enabled")?.InnerText

                                                  If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(port) Then
                                                      serviceNames.Add(name)
                                                      servicePorts.Add(port)
                                                  End If
                                              Next
                                          End If
                                      End If
                                  Catch
                                  End Try

                                  ConfigServiceName.Text = If(serviceNames.Count > 0, String.Join(", ", serviceNames), "Not configured")
                                  ConfigServicePort.Text = If(servicePorts.Count > 0, String.Join(", ", servicePorts), "Not configured")

                                  ' RDP Target Configuration - Get all enabled RDP targets
                                  Dim rdpTargetNames As New List(Of String)
                                  Dim rdpTargetPorts As New List(Of String)

                                  Try
                                      If config IsNot Nothing Then
                                          Dim targetNodes = config.SelectNodes("//RDPTargets/Target")
                                          If targetNodes IsNot Nothing AndAlso targetNodes.Count > 0 Then
                                              For Each targetNode As XmlNode In targetNodes
                                                  Dim name = targetNode.SelectSingleNode("Name")?.InnerText
                                                  Dim port = targetNode.SelectSingleNode("Port")?.InnerText
                                                  Dim enabled = targetNode.SelectSingleNode("Enabled")?.InnerText

                                                  If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(port) Then
                                                      rdpTargetNames.Add(name)
                                                      rdpTargetPorts.Add(port)
                                                  End If
                                              Next
                                          End If
                                      End If
                                  Catch
                                  End Try

                                  ' Display RDP targets
                                  ' Show "Remote Connection" if no enabled targets (assumes default port 3389)
                                  If rdpTargetNames.Count = 0 Then
                                      ' No enabled targets - assume default RDP on 3389
                                      ConfigRdpTargets.Text = "Remote Connection"
                                      ConfigRdpPorts.Text = "3389"
                                  ElseIf rdpTargetNames.Count = 1 AndAlso rdpTargetPorts.Count = 1 AndAlso rdpTargetPorts(0) = "3389" Then
                                      ' Single target on standard port
                                      ConfigRdpTargets.Text = "Remote Connection"
                                      ConfigRdpPorts.Text = "3389"
                                  Else
                                      ' Multiple targets or non-standard port
                                      ConfigRdpTargets.Text = String.Join(", ", rdpTargetNames)
                                      ConfigRdpPorts.Text = String.Join(", ", rdpTargetPorts)
                                  End If

                                  ' Auto-Publish Configuration
                                  Dim autoPublishEnabled = GetConfigValue("//AutoPublish/Enabled")
                                  Dim frequency = GetConfigValue("//AutoPublish/FrequencySeconds")
                                  If autoPublishEnabled.ToLower() = "true" Then
                                      ConfigAutoPublish.Text = $"Enabled ({frequency}s)"
                                  Else
                                      ConfigAutoPublish.Text = "Disabled"
                                  End If
                              End Sub)
        Catch
            ' Ignore errors during config display update
        End Try
    End Sub

    Private Sub StartInternetConnectionMonitor()
        Dim connectionTimer As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(5)}
        AddHandler connectionTimer.Tick, Async Sub()
                                             Dim isConnected = Await IsInternetAvailableAsync()
                                             If isConnected Then
                                                 StartActivityIndicator()
                                                 Await UpdateExternalIPAsync()
                                                 UpdateIPDisplay()
                                                 UpdateConfigDisplay()
                                             Else
                                                 StopActivityIndicator()
                                             End If
                                         End Sub
        connectionTimer.Start()

        ' Initial check
        Task.Run(Async Function()
                     Dim isConnected = Await IsInternetAvailableAsync()
                     Await Dispatcher.InvokeAsync(Sub()
                                                      If isConnected Then
                                                          StartActivityIndicator()
                                                      Else
                                                          StopActivityIndicator()
                                                      End If
                                                  End Sub)
                     Return Nothing
                 End Function)
    End Sub

    Private Sub StartAutoPublishTimer()
        Try
            Dim enabled = GetConfigValue("//AutoPublish/Enabled")
            If enabled.ToLower() <> "true" Then
                Return
            End If

            Dim frequencyStr = GetConfigValue("//AutoPublish/FrequencySeconds")
            Dim frequency As Integer = 300 ' Default 5 minutes

            If Not String.IsNullOrEmpty(frequencyStr) Then
                If Integer.TryParse(frequencyStr, frequency) Then
                    ' Enforce minimum of 60 seconds (1 minute)
                    If frequency < 60 Then
                        frequency = 60
                    End If
                Else
                    frequency = 300 ' Default if parsing fails
                End If
            End If

            Dim publishTimer As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(frequency)}
            AddHandler publishTimer.Tick, Async Sub()
                                              ' Only publish if connected to internet
                                              Dim isConnected = Await IsInternetAvailableAsync()
                                              If isConnected Then
                                                  isManualPublish = False ' Auto-publish, no messages
                                                  Await UpdateExternalIPAsync()
                                                  UpdateIPDisplay()
                                                  Await PublishWebPortalAsync()
                                              End If
                                          End Sub
            publishTimer.Start()

            ' Log the auto-publish status (optional)
            Me.Title = $"{BaseTitle} - Auto-publish every {frequency}s"
        Catch ex As Exception
            ' Auto-publish is optional, don't crash if it fails
        End Try
    End Sub

    Private Sub StartWallpaperRefreshTimer()
        ' Refresh Bing wallpaper daily (checks every hour for 24+ hour interval)
        _wallpaperRefreshTimer = New DispatcherTimer With {.Interval = TimeSpan.FromHours(1)}
        AddHandler _wallpaperRefreshTimer.Tick, Async Sub()
                                                     ' Check if it's been at least 24 hours since last update
                                                     Dim hoursSinceUpdate = (DateTime.Now - _lastWallpaperUpdate).TotalHours
                                                     If hoursSinceUpdate >= 24 Then
                                                         Dim isConnected = Await IsInternetAvailableAsync()
                                                         If isConnected Then
                                                             Await SetBingWallpaperAsync()
                                                             _lastWallpaperUpdate = DateTime.Now
                                                         End If
                                                     End If
                                                 End Sub
        _wallpaperRefreshTimer.Start()
        _lastWallpaperUpdate = DateTime.Now ' Mark initial load time
    End Sub

    Private Async Function GetExternalIPAsync() As Task(Of String)
        Try
            Dim response = Await httpClient.GetStringAsync("https://api.ipify.org")
            Return response.Trim()
        Catch
            Return "Unable to retrieve"
        End Try
    End Function

    Public Function GenerateRdpFile(Optional rdpPort As Integer = 3389) As String
        Dim rdpContent As New StringBuilder()
        rdpContent.AppendLine("screen mode id:i:2")
        rdpContent.AppendLine("use multimon:i:0")
        rdpContent.AppendLine("desktopwidth:i:2880")
        rdpContent.AppendLine("desktopheight:i:1800")
        rdpContent.AppendLine("session bpp:i:32")
        rdpContent.AppendLine("winposstr:s:0,1,0,0,800,600")
        rdpContent.AppendLine("compression:i:1")
        rdpContent.AppendLine("keyboardhook:i:2")
        rdpContent.AppendLine("audiocapturemode:i:0")
        rdpContent.AppendLine("videoplaybackmode:i:1")
        rdpContent.AppendLine("connection type:i:7")
        rdpContent.AppendLine("networkautodetect:i:1")
        rdpContent.AppendLine("bandwidthautodetect:i:1")
        rdpContent.AppendLine("displayconnectionbar:i:1")
        rdpContent.AppendLine("enableworkspacereconnect:i:0")
        rdpContent.AppendLine("remoteappmousemoveinject:i:1")
        rdpContent.AppendLine("disable wallpaper:i:0")
        rdpContent.AppendLine("allow font smoothing:i:0")
        rdpContent.AppendLine("allow desktop composition:i:0")
        rdpContent.AppendLine("disable full window drag:i:1")
        rdpContent.AppendLine("disable menu anims:i:1")
        rdpContent.AppendLine("disable themes:i:0")
        rdpContent.AppendLine("disable cursor setting:i:0")
        rdpContent.AppendLine("bitmapcachepersistenable:i:1")
        rdpContent.AppendLine($"full address:s:{externalIP}:{rdpPort}")
        rdpContent.AppendLine("audiomode:i:0")
        rdpContent.AppendLine("redirectprinters:i:1")
        rdpContent.AppendLine("redirectlocation:i:0")
        rdpContent.AppendLine("redirectcomports:i:0")
        rdpContent.AppendLine("redirectsmartcards:i:1")
        rdpContent.AppendLine("redirectwebauthn:i:1")
        rdpContent.AppendLine("redirectclipboard:i:1")
        rdpContent.AppendLine("redirectposdevices:i:0")
        rdpContent.AppendLine("autoreconnection enabled:i:1")
        rdpContent.AppendLine("authentication level:i:2")
        rdpContent.AppendLine("prompt for credentials:i:0")
        rdpContent.AppendLine("negotiate security layer:i:1")
        rdpContent.AppendLine("remoteapplicationmode:i:0")
        rdpContent.AppendLine("alternate shell:s:")
        rdpContent.AppendLine("shell working directory:s:")
        rdpContent.AppendLine("gatewayhostname:s:")
        rdpContent.AppendLine("gatewayusagemethod:i:4")
        rdpContent.AppendLine("gatewaycredentialssource:i:4")
        rdpContent.AppendLine("gatewayprofileusagemethod:i:0")
        rdpContent.AppendLine("promptcredentialonce:i:0")
        rdpContent.AppendLine("gatewaybrokeringtype:i:0")
        rdpContent.AppendLine("use redirection server name:i:0")
        rdpContent.AppendLine("rdgiskdcproxy:i:0")
        rdpContent.AppendLine("kdcproxyname:s:")
        rdpContent.AppendLine("enablerdsaadauth:i:0")
        Return rdpContent.ToString()
    End Function

    Public Async Function UploadAllRdpFilesAsync(Optional showBlink As Boolean = True) As Task(Of Boolean)
        Dim allSuccessful As Boolean = True
        Dim ftpServer As String = ""

        Try
            If showBlink Then ShowPublishingStatus("Preparing...", "Reading RDP configuration")

            ftpServer = GetConfigValue("//FTP/Server")
            Dim ftpUsername = GetConfigValue("//FTP/Username")
            Dim ftpPassword = GetConfigValue("//FTP/Password")
            Dim remotePath = GetConfigValue("//FTP/RemotePath")

            If String.IsNullOrEmpty(ftpServer) Then
                lastFtpError = "FTP Server address is not configured"
                If showBlink Then UpdateIPDisplay()
                Return False
            End If

            If String.IsNullOrEmpty(ftpUsername) Then
                lastFtpError = "FTP Username is not configured"
                If showBlink Then UpdateIPDisplay()
                Return False
            End If

            ' Remove ftp:// prefix if present
            If ftpServer.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) Then
                ftpServer = ftpServer.Substring(6)
            End If

            ' Get all enabled RDP targets
            Dim rdpTargets As New List(Of (Name As String, Port As Integer, FileName As String))
            Try
                If config IsNot Nothing Then
                    Dim targetNodes = config.SelectNodes("//RDPTargets/Target")
                    If targetNodes IsNot Nothing AndAlso targetNodes.Count > 0 Then
                        For Each targetNode As XmlNode In targetNodes
                            Dim name = targetNode.SelectSingleNode("Name")?.InnerText
                            Dim portStr = targetNode.SelectSingleNode("Port")?.InnerText
                            Dim fileName = targetNode.SelectSingleNode("FileName")?.InnerText
                            Dim enabled = targetNode.SelectSingleNode("Enabled")?.InnerText

                            If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(portStr) AndAlso Not String.IsNullOrEmpty(fileName) Then
                                Dim port As Integer
                                If Integer.TryParse(portStr, port) Then
                                    rdpTargets.Add((name, port, fileName))
                                End If
                            End If
                        Next
                    Else
                        ' No RDPTargets section, use legacy single RDP file
                        Dim legacyFileName = GetConfigValue("//FTP/RdpFileName")
                        If String.IsNullOrEmpty(legacyFileName) Then legacyFileName = "HomeNetwork.rdp"
                        rdpTargets.Add(("Remote Desktop", 3389, legacyFileName))
                    End If
                End If
            Catch
                ' Fallback to default
                rdpTargets.Add(("Remote Desktop", 3389, "HomeNetwork.rdp"))
            End Try

            If rdpTargets.Count = 0 Then
                rdpTargets.Add(("Remote Desktop", 3389, "HomeNetwork.rdp"))
            End If

            If showBlink Then StartUploadBlink()

            ' Upload each RDP file
            For Each target In rdpTargets
                If showBlink Then ShowPublishingStatus("Uploading...", $"RDP: {target.Name} (Port {target.Port})")

                Dim rdpContent = GenerateRdpFile(target.Port)
                Dim ftpUrl = $"ftp://{ftpServer}{remotePath}{target.FileName}"

                Try
                    Dim request As FtpWebRequest = CType(WebRequest.Create(ftpUrl), FtpWebRequest)
                    request.Method = WebRequestMethods.Ftp.UploadFile
                    request.Credentials = New NetworkCredential(ftpUsername, ftpPassword)
                    request.Timeout = 30000
                    request.KeepAlive = False

                    Dim byteArray As Byte() = Encoding.UTF8.GetBytes(rdpContent)
                    request.ContentLength = byteArray.Length

                    Using requestStream As Stream = request.GetRequestStream()
                        Await requestStream.WriteAsync(byteArray, 0, byteArray.Length)
                    End Using

                    Using response As FtpWebResponse = CType(request.GetResponse(), FtpWebResponse)
                        ' Upload successful
                    End Using
                Catch ex As Exception
                    allSuccessful = False
                    lastFtpError = $"Failed to upload {target.FileName}: {ex.Message}"
                End Try

                Await Task.Delay(100)
            Next

            If showBlink Then StopUploadBlink()

            If Not allSuccessful AndAlso showBlink Then UpdateIPDisplay()

            Return allSuccessful

        Catch ex As Exception
            If showBlink Then StopUploadBlink()
            If showBlink Then UpdateIPDisplay()
            lastFtpError = $"Unexpected Error uploading RDP files:{Environment.NewLine}{ex.GetType().Name}: {ex.Message}"
            Return False
        End Try
    End Function

    Public Function SaveRdpFileLocally(Optional filePath As String = Nothing) As Boolean
        Try
            If String.IsNullOrEmpty(filePath) Then
                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HomeNetwork.rdp")
            End If

            Dim rdpContent = GenerateRdpFile()
            File.WriteAllText(filePath, rdpContent)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Function GenerateWebPortalHtml() As String
        Dim passwordHash = ComputeSha256Hash(GetConfigValue("//Security/AccessPassword"))
        Dim rdpFileName = GetConfigValue("//FTP/RdpFileName")

        ' Use default filename if not specified
        If String.IsNullOrEmpty(rdpFileName) Then
            rdpFileName = "HomeNetwork.rdp"
        End If

        ' Determine subtitle based on config file
        Dim subtitle As String = "Secure Access Portal"
        If loadedConfigFile <> "config.xml" AndAlso Not String.IsNullOrEmpty(loadedConfigFile) AndAlso loadedConfigFile <> "Not found" AndAlso loadedConfigFile <> "Error loading" Then
            ' Using PC-specific config file, show PC name
            Dim pcName = GetPCName()
            subtitle = $"{pcName} • Secure Access Portal"
        End If

        ' Get auto-publish frequency for heartbeat indicator
        Dim updateIntervalSeconds As Integer = 300 ' Default 5 minutes
        Dim frequencyStr = GetConfigValue("//AutoPublish/FrequencySeconds")
        If Not String.IsNullOrEmpty(frequencyStr) Then
            If Integer.TryParse(frequencyStr, updateIntervalSeconds) Then
                If updateIntervalSeconds < 60 Then
                    updateIntervalSeconds = 60
                End If
            Else
                updateIntervalSeconds = 300
            End If
        End If

        ' Get all services
        Dim services As New List(Of (Name As String, Url As String))
        Try
            If config IsNot Nothing Then
                Dim serviceNodes = config.SelectNodes("//Services/Service")
                If serviceNodes IsNot Nothing Then
                    For Each serviceNode As XmlNode In serviceNodes
                        Dim name = serviceNode.SelectSingleNode("Name")?.InnerText
                        Dim port = serviceNode.SelectSingleNode("Port")?.InnerText
                        Dim enabled = serviceNode.SelectSingleNode("Enabled")?.InnerText

                        If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(port) AndAlso Not String.IsNullOrEmpty(name) Then
                            services.Add((name, $"http://{externalIP}:{port}"))
                        End If
                    Next
                End If
            End If
        Catch
        End Try

        ' Get all RDP targets
        Dim rdpTargets As New List(Of (Name As String, FileName As String))
        Try
            If config IsNot Nothing Then
                Dim targetNodes = config.SelectNodes("//RDPTargets/Target")
                If targetNodes IsNot Nothing AndAlso targetNodes.Count > 0 Then
                    For Each targetNode As XmlNode In targetNodes
                        Dim name = targetNode.SelectSingleNode("Name")?.InnerText
                        Dim fileName = targetNode.SelectSingleNode("FileName")?.InnerText
                        Dim enabled = targetNode.SelectSingleNode("Enabled")?.InnerText

                        If enabled?.ToLower() = "true" AndAlso Not String.IsNullOrEmpty(name) AndAlso Not String.IsNullOrEmpty(fileName) Then
                            rdpTargets.Add((name, fileName))
                        End If
                    Next
                Else
                    ' No RDPTargets section, use legacy single RDP
                    Dim legacyFileName = GetConfigValue("//FTP/RdpFileName")
                    If String.IsNullOrEmpty(legacyFileName) Then legacyFileName = "HomeNetwork.rdp"
                    rdpTargets.Add(("Remote Desktop", legacyFileName))
                End If
            End If
        Catch
            ' Fallback to default
            rdpTargets.Add(("Remote Desktop", "HomeNetwork.rdp"))
        End Try

        If rdpTargets.Count = 0 Then
            rdpTargets.Add(("Remote Desktop", "HomeNetwork.rdp"))
        End If

        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html>")
        html.AppendLine("<html lang=""en"">")
        html.AppendLine("<head>")
        html.AppendLine("    <meta charset=""UTF-8"">")
        html.AppendLine("    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        html.AppendLine("    <title>Home Network Access</title>")
        html.AppendLine("    <style>")
        html.AppendLine("        * { margin: 0; padding: 0; box-sizing: border-box; }")
        html.AppendLine("        body {")
        html.AppendLine("            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;")
        html.AppendLine("            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);")
        html.AppendLine("            min-height: 100vh;")
        html.AppendLine("            display: flex;")
        html.AppendLine("            justify-content: center;")
        html.AppendLine("            align-items: center;")
        html.AppendLine("            padding: 20px;")
        html.AppendLine("        }")
        html.AppendLine("        .container {")
        html.AppendLine("            background: rgba(255, 255, 255, 0.95);")
        html.AppendLine("            border-radius: 20px;")
        html.AppendLine("            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);")
        html.AppendLine("            padding: 40px;")
        html.AppendLine("            max-width: 600px;")
        html.AppendLine("            width: 100%;")
        html.AppendLine("            backdrop-filter: blur(10px);")
        html.AppendLine("        }")
        html.AppendLine("        h1 {")
        html.AppendLine("            color: #667eea;")
        html.AppendLine("            text-align: center;")
        html.AppendLine("            margin-bottom: 10px;")
        html.AppendLine("            font-size: 2.5em;")
        html.AppendLine("        }")
        html.AppendLine("        .subtitle {")
        html.AppendLine("            text-align: center;")
        html.AppendLine("            color: #666;")
        html.AppendLine("            margin-bottom: 30px;")
        html.AppendLine("            font-size: 0.9em;")
        html.AppendLine("        }")
        html.AppendLine("        .info-card {")
        html.AppendLine("            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);")
        html.AppendLine("            color: white;")
        html.AppendLine("            padding: 20px;")
        html.AppendLine("            border-radius: 15px;")
        html.AppendLine("            margin-bottom: 30px;")
        html.AppendLine("        }")
        html.AppendLine("        .info-row {")
        html.AppendLine("            display: flex;")
        html.AppendLine("            justify-content: space-between;")
        html.AppendLine("            padding: 10px 0;")
        html.AppendLine("            border-bottom: 1px solid rgba(255, 255, 255, 0.2);")
        html.AppendLine("        }")
        html.AppendLine("        .info-row:last-child { border-bottom: none; }")
        html.AppendLine("        .info-label {")
        html.AppendLine("            font-weight: 600;")
        html.AppendLine("            opacity: 0.9;")
        html.AppendLine("        }")
        html.AppendLine("        .info-value {")
        html.AppendLine("            font-family: 'Courier New', monospace;")
        html.AppendLine("            font-weight: bold;")
        html.AppendLine("        }")
        html.AppendLine("        .button-group {")
        html.AppendLine("            display: flex;")
        html.AppendLine("            flex-direction: column;")
        html.AppendLine("            gap: 15px;")
        html.AppendLine("        }")
        html.AppendLine("        .btn {")
        html.AppendLine("            padding: 15px 30px;")
        html.AppendLine("            border: none;")
        html.AppendLine("            border-radius: 10px;")
        html.AppendLine("            font-size: 1.1em;")
        html.AppendLine("            font-weight: 600;")
        html.AppendLine("            cursor: pointer;")
        html.AppendLine("            transition: all 0.3s ease;")
        html.AppendLine("            text-align: center;")
        html.AppendLine("            text-decoration: none;")
        html.AppendLine("            display: block;")
        html.AppendLine("        }")
        html.AppendLine("        .btn-emby {")
        html.AppendLine("            background: linear-gradient(135deg, #52E5A0 0%, #00B386 100%);")
        html.AppendLine("            color: white;")
        html.AppendLine("        }")
        html.AppendLine("        .btn-emby:hover {")
        html.AppendLine("            transform: translateY(-2px);")
        html.AppendLine("            box-shadow: 0 10px 30px rgba(0, 179, 134, 0.4);")
        html.AppendLine("        }")
        html.AppendLine("        .btn-rdp {")
        html.AppendLine("            background: linear-gradient(135deg, #4A90E2 0%, #357ABD 100%);")
        html.AppendLine("            color: white;")
        html.AppendLine("        }")
        html.AppendLine("        .btn-rdp:hover {")
        html.AppendLine("            transform: translateY(-2px);")
        html.AppendLine("            box-shadow: 0 10px 30px rgba(74, 144, 226, 0.4);")
        html.AppendLine("        }")
        html.AppendLine("        .footer {")
        html.AppendLine("            text-align: center;")
        html.AppendLine("            margin-top: 30px;")
        html.AppendLine("            color: #999;")
        html.AppendLine("            font-size: 0.85em;")
        html.AppendLine("        }")
        html.AppendLine("        .password-modal {")
        html.AppendLine("            display: none;")
        html.AppendLine("            position: fixed;")
        html.AppendLine("            top: 0;")
        html.AppendLine("            left: 0;")
        html.AppendLine("            width: 100%;")
        html.AppendLine("            height: 100%;")
        html.AppendLine("            background: rgba(0, 0, 0, 0.7);")
        html.AppendLine("            justify-content: center;")
        html.AppendLine("            align-items: center;")
        html.AppendLine("            z-index: 1000;")
        html.AppendLine("        }")
        html.AppendLine("        .modal-content {")
        html.AppendLine("            background: white;")
        html.AppendLine("            padding: 30px;")
        html.AppendLine("            border-radius: 15px;")
        html.AppendLine("            max-width: 400px;")
        html.AppendLine("            width: 90%;")
        html.AppendLine("        }")
        html.AppendLine("        .modal-content h2 {")
        html.AppendLine("            color: #667eea;")
        html.AppendLine("            margin-bottom: 20px;")
        html.AppendLine("        }")
        html.AppendLine("        .modal-content input {")
        html.AppendLine("            width: 100%;")
        html.AppendLine("            padding: 12px;")
        html.AppendLine("            border: 2px solid #ddd;")
        html.AppendLine("            border-radius: 8px;")
        html.AppendLine("            font-size: 1em;")
        html.AppendLine("            margin-bottom: 15px;")
        html.AppendLine("        }")
        html.AppendLine("        .modal-buttons {")
        html.AppendLine("            display: flex;")
        html.AppendLine("            gap: 10px;")
        html.AppendLine("        }")
        html.AppendLine("        .modal-buttons button {")
        html.AppendLine("            flex: 1;")
        html.AppendLine("        }")
        html.AppendLine("        .error-msg {")
        html.AppendLine("            color: #e74c3c;")
        html.AppendLine("            font-size: 0.9em;")
        html.AppendLine("            margin-top: -10px;")
        html.AppendLine("            margin-bottom: 10px;")
        html.AppendLine("            display: none;")
        html.AppendLine("        }")
        html.AppendLine("        .heartbeat-indicator {")
        html.AppendLine("            display: flex;")
        html.AppendLine("            align-items: center;")
        html.AppendLine("            gap: 8px;")
        html.AppendLine("            font-family: 'Courier New', monospace;")
        html.AppendLine("            font-weight: bold;")
        html.AppendLine("        }")
        html.AppendLine("        .heartbeat-dot {")
        html.AppendLine("            width: 12px;")
        html.AppendLine("            height: 12px;")
        html.AppendLine("            border-radius: 50%;")
        html.AppendLine("            transition: background-color 0.3s ease;")
        html.AppendLine("        }")
        html.AppendLine("        .heartbeat-healthy { background-color: #00B386; box-shadow: 0 0 8px #00B386; }")
        html.AppendLine("        .heartbeat-warning { background-color: #FFA500; box-shadow: 0 0 8px #FFA500; }")
        html.AppendLine("        .heartbeat-stale { background-color: #E74C3C; box-shadow: 0 0 8px #E74C3C; }")
        html.AppendLine("    </style>")
        html.AppendLine("</head>")
        html.AppendLine("<body>")
        html.AppendLine("    <div class=""container"">")
        html.AppendLine("        <h1>🏠 HomeNet Lab</h1>")
        html.AppendLine($"        <div class=""subtitle"">{subtitle}</div>")
        html.AppendLine("        ")
        html.AppendLine("        <div class=""info-card"">")
        html.AppendLine("            <div class=""info-row"">")
        html.AppendLine($"                <span class=""info-label"">External IP:</span>")
        html.AppendLine($"                <span class=""info-value"">{externalIP}</span>")
        html.AppendLine("            </div>")
        html.AppendLine("            <div class=""info-row"">")
        html.AppendLine($"                <span class=""info-label"">Last Updated:</span>")
        html.AppendLine($"                <span class=""info-value"" id=""lastUpdateTime"">{DateTime.Now:yyyy-MM-dd HH:mm:ss}</span>")
        html.AppendLine("            </div>")
        html.AppendLine("            <div class=""info-row"">")
        html.AppendLine($"                <span class=""info-label"">Status:</span>")
        html.AppendLine("                <div class=""heartbeat-indicator"">")
        html.AppendLine("                    <div class=""heartbeat-dot heartbeat-healthy"" id=""heartbeatDot""></div>")
        html.AppendLine("                    <span id=""heartbeatStatus"">Healthy</span>")
        html.AppendLine("                </div>")
        html.AppendLine("            </div>")
        html.AppendLine("        </div>")
        html.AppendLine("        ")
        html.AppendLine("        <div class=""button-group"">")

        ' Add button for each enabled service
        For i As Integer = 0 To services.Count - 1
            Dim svc = services(i)
            Dim icon = If(i = 0, "🎬", "🔌") ' First service gets 🎬, others get 🔌
            html.AppendLine($"            <a href=""#"" class=""btn btn-emby"" onclick=""showPasswordModal('service{i}'); return false;"">")
            html.AppendLine($"                {icon} {svc.Name}")
            html.AppendLine("            </a>")
        Next

        ' Add button for each enabled RDP target
        For i As Integer = 0 To rdpTargets.Count - 1
            Dim rdp = rdpTargets(i)
            html.AppendLine($"            <a href=""#"" class=""btn btn-rdp"" onclick=""showPasswordModal('rdp{i}'); return false;"">")
            html.AppendLine($"                🖥️ {rdp.Name}")
            html.AppendLine("            </a>")
        Next

        html.AppendLine("        </div>")
        html.AppendLine("        ")
        html.AppendLine("        <div class=""footer"">")
        html.AppendLine("            Protected Access • Enter password to continue")
        html.AppendLine("        </div>")
        html.AppendLine("    </div>")
        html.AppendLine("")
        html.AppendLine("    <div id=""passwordModal"" class=""password-modal"">")
        html.AppendLine("        <div class=""modal-content"">")
        html.AppendLine("            <h2>🔒 Authentication Required</h2>")
        html.AppendLine("            <input type=""password"" id=""passwordInput"" placeholder=""Enter password"" onkeypress=""if(event.keyCode==13) verifyPassword()"">")
        html.AppendLine("            <div id=""errorMsg"" class=""error-msg"">Incorrect password. Please try again.</div>")
        html.AppendLine("            <div class=""modal-buttons"">")
        html.AppendLine("                <button class=""btn btn-emby"" onclick=""verifyPassword()"">Submit</button>")
        html.AppendLine("                <button class=""btn"" style=""background: #ccc; color: #333;"" onclick=""closePasswordModal()"">Cancel</button>")
        html.AppendLine("            </div>")
        html.AppendLine("        </div>")
        html.AppendLine("    </div>")
        html.AppendLine("")
        html.AppendLine("    <script>")
        html.AppendLine($"        const CORRECT_PASSWORD_HASH = '{passwordHash}';")
        html.AppendLine($"        const UPDATE_INTERVAL_SECONDS = {updateIntervalSeconds};")
        html.AppendLine($"        const LAST_UPDATE_TIME = new Date('{DateTime.Now:yyyy-MM-dd HH:mm:ss}');")

        ' Add all service URLs as JavaScript array
        html.AppendLine("        const SERVICE_URLS = [")
        For i As Integer = 0 To services.Count - 1
            Dim comma = If(i < services.Count - 1, ",", "")
            html.AppendLine($"            '{services(i).Url}'{comma}")
        Next
        html.AppendLine("        ];")

        ' Add all RDP filenames as JavaScript array
        html.AppendLine("        const RDP_FILENAMES = [")
        For i As Integer = 0 To rdpTargets.Count - 1
            Dim comma = If(i < rdpTargets.Count - 1, ",", "")
            html.AppendLine($"            '{rdpTargets(i).FileName}'{comma}")
        Next
        html.AppendLine("        ];")

        html.AppendLine("        let currentAction = '';")
        html.AppendLine("")
        html.AppendLine("        function showPasswordModal(action) {")
        html.AppendLine("            currentAction = action;")
        html.AppendLine("            document.getElementById('passwordModal').style.display = 'flex';")
        html.AppendLine("            document.getElementById('passwordInput').value = '';")
        html.AppendLine("            document.getElementById('errorMsg').style.display = 'none';")
        html.AppendLine("            document.getElementById('passwordInput').focus();")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        function closePasswordModal() {")
        html.AppendLine("            document.getElementById('passwordModal').style.display = 'none';")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        async function verifyPassword() {")
        html.AppendLine("            const password = document.getElementById('passwordInput').value;")
        html.AppendLine("            const hash = await sha256(password);")
        html.AppendLine("            ")
        html.AppendLine("            if (hash === CORRECT_PASSWORD_HASH) {")
        html.AppendLine("                if (currentAction.startsWith('service')) {")
        html.AppendLine("                    const serviceIndex = parseInt(currentAction.replace('service', ''));")
        html.AppendLine("                    window.location.href = SERVICE_URLS[serviceIndex];")
        html.AppendLine("                } else if (currentAction.startsWith('rdp')) {")
        html.AppendLine("                    const rdpIndex = parseInt(currentAction.replace('rdp', ''));")
        html.AppendLine("                    downloadRdpFile(RDP_FILENAMES[rdpIndex]);")
        html.AppendLine("                }")
        html.AppendLine("                closePasswordModal();")
        html.AppendLine("            } else {")
        html.AppendLine("                document.getElementById('errorMsg').style.display = 'block';")
        html.AppendLine("                document.getElementById('passwordInput').value = '';")
        html.AppendLine("                document.getElementById('passwordInput').focus();")
        html.AppendLine("            }")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        async function sha256(message) {")
        html.AppendLine("            const msgBuffer = new TextEncoder().encode(message);")
        html.AppendLine("            const hashBuffer = await crypto.subtle.digest('SHA-256', msgBuffer);")
        html.AppendLine("            const hashArray = Array.from(new Uint8Array(hashBuffer));")
        html.AppendLine("            const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');")
        html.AppendLine("            return hashHex;")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        function downloadRdpFile(filename) {")
        html.AppendLine("            // Verify file exists on server before downloading")
        html.AppendLine("            fetch(filename, { method: 'HEAD' })")
        html.AppendLine("                .then(response => {")
        html.AppendLine("                    if (response.ok) {")
        html.AppendLine("                        // File exists, proceed with download")
        html.AppendLine("                        const link = document.createElement('a');")
        html.AppendLine("                        link.href = filename;")
        html.AppendLine("                        link.download = filename;")
        html.AppendLine("                        document.body.appendChild(link);")
        html.AppendLine("                        link.click();")
        html.AppendLine("                        document.body.removeChild(link);")
        html.AppendLine("                    } else {")
        html.AppendLine("                        alert('RDP file not found on server. Please contact administrator.');")
        html.AppendLine("                    }")
        html.AppendLine("                })")
        html.AppendLine("                .catch(error => {")
        html.AppendLine("                    // Fallback: try download anyway")
        html.AppendLine("                    const link = document.createElement('a');")
        html.AppendLine("                    link.href = filename;")
        html.AppendLine("                    link.download = filename;")
        html.AppendLine("                    document.body.appendChild(link);")
        html.AppendLine("                    link.click();")
        html.AppendLine("                    document.body.removeChild(link);")
        html.AppendLine("                });")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        // Heartbeat monitoring")
        html.AppendLine("        function updateHeartbeat() {")
        html.AppendLine("            const now = new Date();")
        html.AppendLine("            const secondsSinceUpdate = (now - LAST_UPDATE_TIME) / 1000;")
        html.AppendLine("            const dot = document.getElementById('heartbeatDot');")
        html.AppendLine("            const status = document.getElementById('heartbeatStatus');")
        html.AppendLine("")
        html.AppendLine("            // Remove all status classes")
        html.AppendLine("            dot.classList.remove('heartbeat-healthy', 'heartbeat-warning', 'heartbeat-stale');")
        html.AppendLine("")
        html.AppendLine("            // Healthy: within expected interval")
        html.AppendLine("            if (secondsSinceUpdate < UPDATE_INTERVAL_SECONDS * 1.5) {")
        html.AppendLine("                dot.classList.add('heartbeat-healthy');")
        html.AppendLine("                status.textContent = 'Healthy';")
        html.AppendLine("            }")
        html.AppendLine("            // Warning: overdue but not critical (1.5x - 2x interval)")
        html.AppendLine("            else if (secondsSinceUpdate < UPDATE_INTERVAL_SECONDS * 2) {")
        html.AppendLine("                dot.classList.add('heartbeat-warning');")
        html.AppendLine("                status.textContent = 'Warning';")
        html.AppendLine("            }")
        html.AppendLine("            // Stale: very overdue (2x+ interval)")
        html.AppendLine("            else {")
        html.AppendLine("                dot.classList.add('heartbeat-stale');")
        html.AppendLine("                status.textContent = 'Stale';")
        html.AppendLine("            }")
        html.AppendLine("        }")
        html.AppendLine("")
        html.AppendLine("        // Update heartbeat every 10 seconds")
        html.AppendLine("        setInterval(updateHeartbeat, 10000);")
        html.AppendLine("        updateHeartbeat(); // Initial check")
        html.AppendLine("")
        html.AppendLine("        // Auto-refresh page synchronized with expected update time")
        html.AppendLine("        // Calculate when the next update is expected based on last publish time")
        html.AppendLine("        const timeSinceLastUpdate = (new Date() - LAST_UPDATE_TIME) / 1000; // seconds")
        html.AppendLine("        const timeUntilNextUpdate = UPDATE_INTERVAL_SECONDS - timeSinceLastUpdate;")
        html.AppendLine("        // Add 5 second buffer after expected update, minimum 10 seconds")
        html.AppendLine("        const refreshTime = Math.max(timeUntilNextUpdate + 5, 10);")
        html.AppendLine("        ")
        html.AppendLine("        setTimeout(() => {")
        html.AppendLine("            location.reload();")
        html.AppendLine("        }, refreshTime * 1000);")
        html.AppendLine("    </script>")
        html.AppendLine("</body>")
        html.AppendLine("</html>")

        Return html.ToString()
    End Function

    Private Function ComputeSha256Hash(rawData As String) As String
        If String.IsNullOrEmpty(rawData) Then Return ""

        Using sha256Hash = System.Security.Cryptography.SHA256.Create()
            Dim bytes As Byte() = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData))
            Dim builder As New StringBuilder()
            For i As Integer = 0 To bytes.Length - 1
                builder.Append(bytes(i).ToString("x2"))
            Next
            Return builder.ToString()
        End Using
    End Function

    Public Async Function PublishWebPortalAsync() As Task(Of Boolean)
        Try
            StartUploadBlink()
            ShowPublishingStatus("Publishing...", "Updating external IP")

            ' Update external IP first
            Await UpdateExternalIPAsync()
            Await Task.Delay(300) ' Brief pause for visual feedback

            ShowPublishingStatus("Publishing...", "Uploading RDP files")
            ' Upload all RDP files (without separate blink control)
            Dim rdpSuccess = Await UploadAllRdpFilesAsync(False)
            If Not rdpSuccess Then
                StopUploadBlink()
                ShowPublishingStatus("Failed", "RDP upload error")
                Await Task.Delay(2000)
                UpdateIPDisplay()
                Return False
            End If

            Await Task.Delay(300) ' Brief pause for visual feedback

            ShowPublishingStatus("Publishing...", "Uploading HTML portal")

            ' Then upload HTML portal
            Dim ftpServer = GetConfigValue("//FTP/Server")
            Dim ftpUsername = GetConfigValue("//FTP/Username")
            Dim ftpPassword = GetConfigValue("//FTP/Password")
            Dim remotePath = GetConfigValue("//FTP/RemotePath")
            Dim htmlFileName = GetConfigValue("//FTP/HtmlFileName")

            ' Use default filename if not specified
            If String.IsNullOrEmpty(htmlFileName) Then
                htmlFileName = "index.html"
            End If

            If String.IsNullOrEmpty(ftpServer) Then
                lastFtpError = "FTP Server address is not configured in config.xml"
                StopUploadBlink()
                ShowPublishingStatus("Failed", "Server not configured")
                Await Task.Delay(2000)
                UpdateIPDisplay()
                Return False
            End If

            If String.IsNullOrEmpty(ftpUsername) Then
                lastFtpError = "FTP Username is not configured in config.xml"
                StopUploadBlink()
                ShowPublishingStatus("Failed", "Username not configured")
                Await Task.Delay(2000)
                UpdateIPDisplay()
                Return False
            End If

            ' Remove ftp:// prefix if present (we'll add it ourselves)
            If ftpServer.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) Then
                ftpServer = ftpServer.Substring(6)
            End If

            Dim htmlContent = GenerateWebPortalHtml()
            Dim ftpUrl = $"ftp://{ftpServer}{remotePath}{htmlFileName}"

            Dim request As FtpWebRequest = CType(WebRequest.Create(ftpUrl), FtpWebRequest)
            request.Method = WebRequestMethods.Ftp.UploadFile
            request.Credentials = New NetworkCredential(ftpUsername, ftpPassword)
            request.Timeout = 30000 ' 30 seconds
            request.KeepAlive = False

            Dim byteArray As Byte() = Encoding.UTF8.GetBytes(htmlContent)
            request.ContentLength = byteArray.Length

            Using requestStream As Stream = request.GetRequestStream()
                Await requestStream.WriteAsync(byteArray, 0, byteArray.Length)
            End Using

            Using response As FtpWebResponse = CType(request.GetResponse(), FtpWebResponse)
                ' Upload successful
                lastFtpError = "" ' Clear any previous errors
            End Using

            StopUploadBlink()
            ShowPublishingStatus("Complete!", $"Published to {ftpServer}")
            Await Task.Delay(2000) ' Show completion message
            UpdateIPDisplay()
            Return True

        Catch webEx As WebException
            StopUploadBlink()
            ShowPublishingStatus("Failed", "Upload error")
            Task.Delay(2000).Wait() ' Can't await in catch
            UpdateIPDisplay()

            Dim errorMsg As New StringBuilder()
            errorMsg.AppendLine($"HTML Portal Upload Failed: {GetConfigValue("//FTP/HtmlFileName")}")
            errorMsg.AppendLine()

            If webEx.Response IsNot Nothing Then
                Dim ftpResponse As FtpWebResponse = CType(webEx.Response, FtpWebResponse)
                errorMsg.AppendLine($"FTP Status Code: {ftpResponse.StatusCode}")
                errorMsg.AppendLine($"FTP Status: {ftpResponse.StatusDescription}")
            End If

            Select Case webEx.Status
                Case WebExceptionStatus.ConnectFailure
                    errorMsg.AppendLine("Cannot connect to FTP server")
                    errorMsg.AppendLine($"Server: {GetConfigValue("//FTP/Server")}")
                    errorMsg.AppendLine("Check: Server address, firewall, internet connection")

                Case WebExceptionStatus.NameResolutionFailure
                    errorMsg.AppendLine("Cannot resolve FTP server hostname")
                    errorMsg.AppendLine($"Server: {GetConfigValue("//FTP/Server")}")
                    errorMsg.AppendLine("Check: Server address spelling, DNS settings")

                Case WebExceptionStatus.Timeout
                    errorMsg.AppendLine("Connection timed out (30 seconds)")
                    errorMsg.AppendLine("Check: Server is online, firewall settings")

                Case WebExceptionStatus.ProtocolError
                    errorMsg.AppendLine("FTP Protocol Error")
                    If webEx.Response IsNot Nothing Then
                        Dim ftpResponse As FtpWebResponse = CType(webEx.Response, FtpWebResponse)
                        If ftpResponse.StatusCode = FtpStatusCode.NotLoggedIn Then
                            errorMsg.AppendLine("Authentication failed - Invalid username or password")
                        ElseIf ftpResponse.StatusCode = FtpStatusCode.ActionNotTakenFileUnavailable Then
                            errorMsg.AppendLine("Cannot access remote path")
                            errorMsg.AppendLine($"Path: {GetConfigValue("//FTP/RemotePath")}")
                            errorMsg.AppendLine("Check: Path exists and has write permissions")
                        End If
                    End If

                Case Else
                    errorMsg.AppendLine($"Network Error: {webEx.Status}")
                    errorMsg.AppendLine($"Message: {webEx.Message}")
            End Select

            lastFtpError = errorMsg.ToString()
            Return False

        Catch ex As Exception
            StopUploadBlink()
            ShowPublishingStatus("Failed", "Unexpected error")
            Task.Delay(2000).Wait() ' Can't await in catch
            UpdateIPDisplay()
            lastFtpError = $"Unexpected Error uploading HTML portal:{Environment.NewLine}{ex.GetType().Name}: {ex.Message}"
            Return False
        End Try
    End Function

    ' Service Editor Handlers
    Private Sub AddServiceButton_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New ServiceDialog()
        If dialog.ShowDialog() = True Then
            servicesCollection.Add(New ServiceItem With {
                .Name = dialog.ServiceName,
                .Port = dialog.ServicePort,
                .Enabled = dialog.ServiceEnabled
            })
        End If
    End Sub

    Private Sub EditServiceButton_Click(sender As Object, e As RoutedEventArgs)
        If EditServicesList.SelectedItem Is Nothing Then
            MessageBox.Show("Please select a service to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim selectedService = CType(EditServicesList.SelectedItem, ServiceItem)
        Dim dialog As New ServiceDialog() With {
            .ServiceName = selectedService.Name,
            .ServicePort = selectedService.Port,
            .ServiceEnabled = selectedService.Enabled
        }

        If dialog.ShowDialog() = True Then
            selectedService.Name = dialog.ServiceName
            selectedService.Port = dialog.ServicePort
            selectedService.Enabled = dialog.ServiceEnabled
            selectedService.NotifyPropertyChanged("DisplayText")
        End If
    End Sub

    Private Sub RemoveServiceButton_Click(sender As Object, e As RoutedEventArgs)
        If EditServicesList.SelectedItem Is Nothing Then
            MessageBox.Show("Please select a service to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim result = MessageBox.Show("Remove this service?", "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question)
        If result = MessageBoxResult.Yes Then
            servicesCollection.Remove(CType(EditServicesList.SelectedItem, ServiceItem))
        End If
    End Sub

    ' RDP Target Editor Handlers
    Private Sub AddRdpTargetButton_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog As New RdpTargetDialog()
        If dialog.ShowDialog() = True Then
            rdpTargetsCollection.Add(New RdpTargetItem With {
                .Name = dialog.TargetName,
                .Port = dialog.TargetPort,
                .FileName = dialog.TargetFileName,
                .Enabled = dialog.TargetEnabled
            })
        End If
    End Sub

    Private Sub EditRdpTargetButton_Click(sender As Object, e As RoutedEventArgs)
        If EditRdpTargetsList.SelectedItem Is Nothing Then
            MessageBox.Show("Please select an RDP target to edit.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim selectedTarget = CType(EditRdpTargetsList.SelectedItem, RdpTargetItem)
        Dim dialog As New RdpTargetDialog() With {
            .TargetName = selectedTarget.Name,
            .TargetPort = selectedTarget.Port,
            .TargetFileName = selectedTarget.FileName,
            .TargetEnabled = selectedTarget.Enabled
        }

        If dialog.ShowDialog() = True Then
            selectedTarget.Name = dialog.TargetName
            selectedTarget.Port = dialog.TargetPort
            selectedTarget.FileName = dialog.TargetFileName
            selectedTarget.Enabled = dialog.TargetEnabled
            selectedTarget.NotifyPropertyChanged("DisplayText")
        End If
    End Sub

    Private Sub RemoveRdpTargetButton_Click(sender As Object, e As RoutedEventArgs)
        If EditRdpTargetsList.SelectedItem Is Nothing Then
            MessageBox.Show("Please select an RDP target to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim result = MessageBox.Show("Remove this RDP target?", "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question)
        If result = MessageBoxResult.Yes Then
            rdpTargetsCollection.Remove(CType(EditRdpTargetsList.SelectedItem, RdpTargetItem))
        End If
    End Sub
End Class

' ServiceItem class for data binding
Public Class ServiceItem
    Implements INotifyPropertyChanged

    Private _name As String
    Private _port As Integer
    Private _enabled As Boolean

    Public Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
            OnPropertyChanged("Name")
            OnPropertyChanged("DisplayText")
        End Set
    End Property

    Public Property Port As Integer
        Get
            Return _port
        End Get
        Set(value As Integer)
            _port = value
            OnPropertyChanged("Port")
            OnPropertyChanged("DisplayText")
        End Set
    End Property

    Public Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
            _enabled = value
            OnPropertyChanged("Enabled")
        End Set
    End Property

    Public ReadOnly Property DisplayText As String
        Get
            Return $"{Name} (Port {Port})"
        End Get
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class

' RdpTargetItem class for data binding
Public Class RdpTargetItem
    Implements INotifyPropertyChanged

    Private _name As String
    Private _port As Integer
    Private _fileName As String
    Private _enabled As Boolean

    Public Property Name As String
        Get
            Return _name
        End Get
        Set(value As String)
            _name = value
            OnPropertyChanged("Name")
            OnPropertyChanged("DisplayText")
        End Set
    End Property

    Public Property Port As Integer
        Get
            Return _port
        End Get
        Set(value As Integer)
            _port = value
            OnPropertyChanged("Port")
            OnPropertyChanged("DisplayText")
        End Set
    End Property

    Public Property FileName As String
        Get
            Return _fileName
        End Get
        Set(value As String)
            _fileName = value
            OnPropertyChanged("FileName")
            OnPropertyChanged("DisplayText")
        End Set
    End Property

    Public Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
            _enabled = value
            OnPropertyChanged("Enabled")
        End Set
    End Property

    Public ReadOnly Property DisplayText As String
        Get
            Return $"{Name} (Port {Port}) - {FileName}"
        End Get
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class
