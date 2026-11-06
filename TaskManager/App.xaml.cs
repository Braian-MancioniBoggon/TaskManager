using System;
using WpfApplication = System.Windows.Application;
using Microsoft.Windows.AppNotifications;

namespace TaskManager;

public partial class App : WpfApplication
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "No se pudieron inicializar las notificaciones de Windows:\n\n" + ex.Message,
                "NoSeMeOlvida",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning
            );
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
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