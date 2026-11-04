
using System.Windows;
using Microsoft.Windows.AppNotifications;

namespace TaskManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "No se pudieron inicializar las notificaciones de Windows:\n\n" + ex.Message,
                "TaskManager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
            // Evita que un error al cerrar afecte la aplicación.
        }

        base.OnExit(e);
    }
}
