
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace TaskManager
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Tarea> _pendientes = new();
        private readonly ObservableCollection<Tarea> _completadas = new();

        private readonly DispatcherTimer _temporizador = new()
        {
            Interval = TimeSpan.FromMinutes(30)
        };

        private NotifyIcon? _iconoBandeja;
        private ContextMenuStrip? _menuBandeja;

        private bool _salidaDefinitiva = false;
        private bool _mostrarCompletadas = false;
        private bool _notificacionesActivas = true;

        private const string ClaveInicioWindows =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string NombreValorInicioWindows = "TaskManager";

        public MainWindow()
        {
            InitializeComponent();

            _temporizador.Tick += Temporizador_Tick;

            try
            {
                BaseDatos.Inicializar();

                // CARGAR PREFERENCIAS GUARDADAS
                var configuracion = BaseDatos.ObtenerConfiguracion();

                _notificacionesActivas =
                    configuracion.NotificacionesActivas;

                _temporizador.Interval =
                    TimeSpan.FromMinutes(configuracion.IntervaloMinutos);

                CargarTareasDesdeBaseDatos();

                MostrarPendientes();

                ActualizarContadores();

                // INICIALIZAR ICONO DE LA BANDEJA
                InicializarBandeja();

                // SI WINDOWS INICIÓ LA APLICACIÓN, OCULTAR LA VENTANA
                if (Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argumento => string.Equals(
                        argumento,
                        "--background",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Loaded += (_, _) => Hide();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo iniciar la base de datos.\n\n{ex.Message}",
                    "Error de base de datos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                _salidaDefinitiva = true;
                Close();
            }
        }

        // CONSULTAR SI TASKMANAGER ESTÁ CONFIGURADO PARA INICIAR CON WINDOWS
        private bool EstaConfiguradoInicioWindows()
        {
            using var clave = Registry.CurrentUser.OpenSubKey(
                ClaveInicioWindows,
                false);

            return clave?.GetValue(NombreValorInicioWindows) != null;
        }

        // ACTIVAR O DESACTIVAR EL INICIO CON WINDOWS
        private void ConfigurarInicioWindows(bool activar)
        {
            using var clave = Registry.CurrentUser.CreateSubKey(
                ClaveInicioWindows);

            if (clave == null)
            {
                throw new InvalidOperationException(
                    "No se pudo acceder al Registro de Windows.");
            }

            if (activar)
            {
                string? ruta = Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(ruta))
                {
                    throw new InvalidOperationException(
                        "No se pudo obtener la ruta del ejecutable.");
                }

                clave.SetValue(
                    NombreValorInicioWindows,
                    $"\"{ruta}\" --background");
            }
            else
            {
                clave.DeleteValue(
                    NombreValorInicioWindows,
                    false);
            }
        }

        // INICIALIZAR BANDEJA DE WINDOWS
        private void InicializarBandeja()
        {
            _menuBandeja = new ContextMenuStrip();

            var opcionAbrir = new ToolStripMenuItem("Abrir TaskManager");
            opcionAbrir.Click += (_, _) => MostrarVentana();

            var opcionSalir = new ToolStripMenuItem("Salir");
            opcionSalir.Click += (_, _) => SalirDefinitivamente();

            _menuBandeja.Items.Add(opcionAbrir);
            _menuBandeja.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            _menuBandeja.Items.Add(opcionSalir);

            _iconoBandeja = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "TaskManager",
                ContextMenuStrip = _menuBandeja,
                Visible = true
            };

            _iconoBandeja.DoubleClick += (_, _) => MostrarVentana();
        }

        // MOSTRAR VENTANA DESDE LA BANDEJA
        private void MostrarVentana()
        {
            Show();

            WindowState = WindowState.Normal;

            ShowInTaskbar = true;

            Activate();

            Topmost = true;
            Topmost = false;

            Focus();
        }

        // CERRAR DEFINITIVAMENTE LA APLICACIÓN
        private void SalirDefinitivamente()
        {
            _salidaDefinitiva = true;

            if (_iconoBandeja != null)
            {
                _iconoBandeja.Visible = false;
                _iconoBandeja.Dispose();
                _iconoBandeja = null;
            }

            _menuBandeja?.Dispose();
            _menuBandeja = null;

            Close();
        }

        // TEMPORIZADOR DE NOTIFICACIONES
        private void Temporizador_Tick(object? sender, EventArgs e)
        {
            if (!_notificacionesActivas || _pendientes.Count == 0)
            {
                _temporizador.Stop();
                return;
            }

            try
            {
                string mensaje = _pendientes.Count == 1
                    ? "Tenés 1 tarea pendiente. ¡No te olvides de completarla!"
                    : $"Tenés {_pendientes.Count} tareas pendientes. ¡No te olvides de ellas!";

                ServicioNotificaciones.MostrarNotificacion(
                    "Recordatorio de tareas",
                    mensaje);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Error al mostrar la notificación: {ex.Message}");
            }
        }

        // ACTIVAR O DESACTIVAR RECORDATORIOS
        private void ActualizarEstadoRecordatorios()
        {
            if (_notificacionesActivas && _pendientes.Count > 0)
            {
                if (!_temporizador.IsEnabled)
                    _temporizador.Start();
            }
            else
            {
                _temporizador.Stop();
            }
        }

        // CARGAR TAREAS DESDE SQLITE
        private void CargarTareasDesdeBaseDatos()
        {
            _pendientes.Clear();
            _completadas.Clear();

            foreach (var tarea in BaseDatos.ObtenerTareas(false))
            {
                _pendientes.Add(tarea);
            }

            foreach (var tarea in BaseDatos.ObtenerTareas(true))
            {
                _completadas.Add(tarea);
            }

            ActualizarEstadoRecordatorios();
        }

        // NAVEGACIÓN
        private void SeleccionarSeccion(Button botonSeleccionado)
        {
            BtnPendientes.Background = Brushes.Transparent;
            BtnCompletadas.Background = Brushes.Transparent;
            BtnConfiguracion.Background = Brushes.Transparent;

            var colorTexto = new SolidColorBrush(
                Color.FromRgb(174, 180, 193));

            BtnPendientes.Foreground = colorTexto;
            BtnCompletadas.Foreground = colorTexto;
            BtnConfiguracion.Foreground = colorTexto;

            botonSeleccionado.Background = new SolidColorBrush(
                Color.FromRgb(48, 59, 89));

            botonSeleccionado.Foreground = Brushes.White;
        }

        // MOSTRAR ÁREA DE TAREAS
        private void MostrarPanelTareas()
        {
            PanelAccionesEncabezado.Visibility = Visibility.Visible;
            PanelResumen.Visibility = Visibility.Visible;
            PanelLista.Visibility = Visibility.Visible;
            PanelConfiguracion.Visibility = Visibility.Collapsed;
        }

        // MOSTRAR PENDIENTES
        private void MostrarPendientes()
        {
            _mostrarCompletadas = false;

            ListaTareas.ItemsSource = _pendientes;

            TxtTituloLista.Text = "Lista de tareas";

            BtnBorrarTodasCompletadas.Visibility = Visibility.Collapsed;

            TxtListaVacia.Text = "¡No tenés tareas pendientes!";

            TxtListaVacia.Visibility = _pendientes.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // MOSTRAR COMPLETADAS
        private void MostrarCompletadas()
        {
            _mostrarCompletadas = true;

            ListaTareas.ItemsSource = _completadas;

            TxtTituloLista.Text = "Historial de tareas";

            BtnBorrarTodasCompletadas.Visibility = _completadas.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            TxtListaVacia.Text = "Todavía no completaste ninguna tarea.";

            TxtListaVacia.Visibility = _completadas.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // BORRAR TODAS LAS TAREAS COMPLETADAS
        private void BtnBorrarTodasCompletadas_Click(object sender, RoutedEventArgs e)
        {
            if (_completadas.Count == 0)
                return;

            int cantidad = _completadas.Count;

            var resultado = MessageBox.Show(
                $"¿Estás seguro de que querés eliminar las {cantidad} tareas completadas?\n\n" +
                "Esta acción no se puede deshacer. Las tareas pendientes no se modificarán.",
                "Borrar historial de tareas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado != MessageBoxResult.Yes)
                return;

            try
            {
                BaseDatos.EliminarTodasLasCompletadas();

                _completadas.Clear();

                ActualizarContadores();
                MostrarCompletadas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudieron eliminar las tareas completadas.\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ACTUALIZAR CONTADORES
        private void ActualizarContadores()
        {
            TxtCantidadPendientes.Text =
                _pendientes.Count.ToString("00");

            TxtCantidadCompletadas.Text =
                _completadas.Count.ToString("00");

            TxtCantidadTotal.Text =
                (_pendientes.Count + _completadas.Count).ToString("00");
        }

        // BOTÓN PENDIENTES
        private void BtnPendientes_Click(object sender, RoutedEventArgs e)
        {
            SeleccionarSeccion(BtnPendientes);

            MostrarPanelTareas();

            TxtTitulo.Text = "Mis tareas";

            TxtSubtitulo.Text =
                "Organizá tus actividades y mantené todo bajo control.";

            MostrarPendientes();
        }

        // BOTÓN COMPLETADAS
        private void BtnCompletadas_Click(object sender, RoutedEventArgs e)
        {
            SeleccionarSeccion(BtnCompletadas);

            MostrarPanelTareas();

            TxtTitulo.Text = "Tareas completadas";

            TxtSubtitulo.Text =
                "Consultá el historial de tus actividades finalizadas.";

            MostrarCompletadas();
        }

        // BOTÓN CONFIGURACIÓN
        private void BtnConfiguracion_Click(object sender, RoutedEventArgs e)
        {
            SeleccionarSeccion(BtnConfiguracion);

            TxtTitulo.Text = "Configuración";

            TxtSubtitulo.Text =
                "Personalizá el funcionamiento de TaskManager.";

            PanelAccionesEncabezado.Visibility = Visibility.Collapsed;
            PanelResumen.Visibility = Visibility.Collapsed;
            PanelLista.Visibility = Visibility.Collapsed;
            PanelConfiguracion.Visibility = Visibility.Visible;

            CargarConfiguracionEnPantalla();
        }

        // CREAR TAREA
        private void BtnNuevaTarea_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new NuevaTareaWindow
            {
                Owner = this
            };

            if (ventana.ShowDialog() == true &&
                ventana.TareaCreada != null)
            {
                try
                {
                    var tarea = ventana.TareaCreada;

                    BaseDatos.GuardarTarea(tarea);

                    _pendientes.Add(tarea);

                    ActualizarEstadoRecordatorios();
                    ActualizarContadores();

                    SeleccionarSeccion(BtnPendientes);

                    MostrarPanelTareas();

                    TxtTitulo.Text = "Mis tareas";

                    TxtSubtitulo.Text =
                        "Organizá tus actividades y mantené todo bajo control.";

                    MostrarPendientes();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"No se pudo guardar la tarea.\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // COMPLETAR TAREA
        private void BtnCompletar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button boton ||
                boton.Tag is not Tarea tarea)
            {
                return;
            }

            try
            {
                BaseDatos.MarcarComoCompletada(tarea.Id);

                tarea.Completada = true;
                tarea.FechaCompletada = DateTime.Now;

                _pendientes.Remove(tarea);

                _completadas.Insert(0, tarea);

                ActualizarEstadoRecordatorios();
                ActualizarContadores();

                if (_mostrarCompletadas)
                    MostrarCompletadas();
                else
                    MostrarPendientes();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo completar la tarea.\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // EDITAR TAREA
        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button boton ||
                boton.Tag is not Tarea tarea)
            {
                return;
            }

            var ventana = new NuevaTareaWindow(tarea)
            {
                Owner = this
            };

            if (ventana.ShowDialog() != true ||
                ventana.TareaCreada == null)
            {
                return;
            }

            try
            {
                BaseDatos.ActualizarTarea(ventana.TareaCreada);

                CargarTareasDesdeBaseDatos();

                if (_mostrarCompletadas)
                    MostrarCompletadas();
                else
                    MostrarPendientes();

                ActualizarContadores();
            }
            catch (Exception ex)
            {
                CargarTareasDesdeBaseDatos();

                if (_mostrarCompletadas)
                    MostrarCompletadas();
                else
                    MostrarPendientes();

                MessageBox.Show(
                    $"No se pudo actualizar la tarea.\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ELIMINAR TAREA
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button boton ||
                boton.Tag is not Tarea tarea)
            {
                return;
            }

            var resultado = MessageBox.Show(
                $"¿Estás seguro de que querés eliminar esta tarea?\n\n{tarea.Titulo}\n\nEsta acción no se puede deshacer.",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado != MessageBoxResult.Yes)
                return;

            try
            {
                BaseDatos.EliminarTarea(tarea.Id);

                _pendientes.Remove(tarea);
                _completadas.Remove(tarea);

                ActualizarEstadoRecordatorios();
                ActualizarContadores();

                if (_mostrarCompletadas)
                    MostrarCompletadas();
                else
                    MostrarPendientes();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo eliminar la tarea.\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // CARGAR CONFIGURACIÓN EN PANTALLA
        private void CargarConfiguracionEnPantalla()
        {
            var configuracion = BaseDatos.ObtenerConfiguracion();

            ChkNotificacionesActivas.IsChecked =
                configuracion.NotificacionesActivas;

            foreach (ComboBoxItem item in CmbIntervaloNotificaciones.Items)
            {
                if (int.TryParse(item.Tag?.ToString(), out int intervalo) &&
                    intervalo == configuracion.IntervaloMinutos)
                {
                    CmbIntervaloNotificaciones.SelectedItem = item;
                    break;
                }
            }

            ChkIniciarConWindows.IsChecked =
                EstaConfiguradoInicioWindows();

            TxtEstadoConfiguracion.Text = "";
        }

        // GUARDAR CONFIGURACIÓN
        private void BtnGuardarConfiguracion_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                bool notificacionesActivas =
                    ChkNotificacionesActivas.IsChecked == true;

                if (CmbIntervaloNotificaciones.SelectedItem
                    is not ComboBoxItem item ||
                    !int.TryParse(item.Tag?.ToString(), out int intervalo))
                {
                    MessageBox.Show(
                        "Seleccioná un intervalo válido.",
                        "Configuración",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // GUARDAR EN SQLITE
                BaseDatos.GuardarConfiguracion(
                    notificacionesActivas,
                    intervalo);

                // CONFIGURAR INICIO CON WINDOWS
                bool iniciarConWindows =
                    ChkIniciarConWindows.IsChecked == true;

                ConfigurarInicioWindows(iniciarConWindows);

                // ACTUALIZAR PREFERENCIAS EN MEMORIA
                _notificacionesActivas = notificacionesActivas;

                // REINICIAR TEMPORIZADOR CON EL NUEVO INTERVALO
                _temporizador.Stop();

                _temporizador.Interval =
                    TimeSpan.FromMinutes(intervalo);

                // APLICAR CONFIGURACIÓN INMEDIATAMENTE
                ActualizarEstadoRecordatorios();

                TxtEstadoConfiguracion.Text =
                    "✓ Configuración guardada correctamente.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo guardar la configuración:\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // OCULTAR VENTANA AL CERRAR CON LA X
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_salidaDefinitiva)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            base.OnClosing(e);
        }

        // LIMPIAR RECURSOS AL CERRAR DEFINITIVAMENTE
        protected override void OnClosed(EventArgs e)
        {
            _temporizador.Stop();

            if (_iconoBandeja != null)
            {
                _iconoBandeja.Visible = false;
                _iconoBandeja.Dispose();
                _iconoBandeja = null;
            }

            _menuBandeja?.Dispose();
            _menuBandeja = null;

            base.OnClosed(e);
        }
    }
}
