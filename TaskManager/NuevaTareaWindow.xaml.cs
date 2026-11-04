
using System;
using System.Windows;

namespace TaskManager
{
    public partial class NuevaTareaWindow : Window
    {
        public Tarea? TareaCreada { get; private set; }

        private readonly Tarea? _tareaOriginal;

        public NuevaTareaWindow(Tarea? tarea = null)
        {
            InitializeComponent();

            _tareaOriginal = tarea;

            if (_tareaOriginal != null)
            {
                Title = "Editar tarea";

                TareaCreada = null;

                TxtTitulo.Text = _tareaOriginal.Titulo;
                TxtDescripcion.Text = _tareaOriginal.Descripcion;
                FechaVencimiento.SelectedDate =
                    _tareaOriginal.FechaVencimiento;

                TxtEncabezado.Text = "Editar tarea";
                TxtSubtitulo.Text = "Modificá los datos de la actividad.";
                BtnGuardar.Content = "Guardar cambios";
            }
            else
            {
                TxtTitulo.Focus();
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string titulo = TxtTitulo.Text.Trim();

            if (string.IsNullOrWhiteSpace(titulo))
            {
                MessageBox.Show(
                    "Ingresá un título para la tarea.",
                    "Dato obligatorio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                TxtTitulo.Focus();
                return;
            }

            if (_tareaOriginal == null)
            {
                TareaCreada = new Tarea
                {
                    Titulo = titulo,
                    Descripcion = TxtDescripcion.Text.Trim(),
                    FechaCreacion = DateTime.Now,
                    FechaVencimiento = FechaVencimiento.SelectedDate
                };
            }
            else
            {
                _tareaOriginal.Titulo = titulo;
                _tareaOriginal.Descripcion = TxtDescripcion.Text.Trim();
                _tareaOriginal.FechaVencimiento =
                    FechaVencimiento.SelectedDate;

                TareaCreada = _tareaOriginal;
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
