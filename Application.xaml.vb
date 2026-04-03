Imports System.Windows
Imports System.Threading

Partial Public Class Application
    Inherits System.Windows.Application

    Private appMutex As Mutex
    Private Const AppMutexName As String = "HomeNetLab_SingleInstance_Mutex_B4E9D1A2"

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        ' Check for existing instance
        Dim createdNew As Boolean
        appMutex = New Mutex(True, AppMutexName, createdNew)

        If Not createdNew Then
            ' Another instance is already running
            MessageBox.Show("HomeNet Lab is already running!" & Environment.NewLine & Environment.NewLine &
                           "Check the system tray for the 🏠 icon." & Environment.NewLine &
                           "Right-click the icon to access the application.",
                           "Already Running",
                           MessageBoxButton.OK,
                           MessageBoxImage.Information)
            Current.Shutdown()
            Return
        End If

        ' Show splash screen
        Dim splash = New SplashScreen("SplashLab.png")
        splash.Show(False)
        System.Threading.Thread.Sleep(1500)
        splash.Close(TimeSpan.Zero)

        MyBase.OnStartup(e)
    End Sub

    Protected Overrides Sub OnExit(e As ExitEventArgs)
        ' Release the mutex
        If appMutex IsNot Nothing Then
            appMutex.ReleaseMutex()
            appMutex.Dispose()
        End If

        MyBase.OnExit(e)
    End Sub
End Class
