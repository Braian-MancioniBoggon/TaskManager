
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace TaskManager;

public static class ServicioNotificaciones
{
    public static void MostrarNotificacion(string titulo, string mensaje)
    {
        var notificacion = new AppNotificationBuilder()
            .AddText(titulo)
            .AddText(mensaje)
            .BuildNotification();

        AppNotificationManager.Default.Show(notificacion);
    }

    public static void MostrarNotificacionPrueba()
    {
        MostrarNotificacion(
            "TaskManager",
            "Las notificaciones están funcionando correctamente."
        );
    }
}
