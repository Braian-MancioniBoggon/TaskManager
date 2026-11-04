
using System;

namespace TaskManager
{
    public class Tarea
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Titulo { get; set; } = "";

        public string Descripcion { get; set; } = "";

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        public DateTime? FechaVencimiento { get; set; }

        public bool Completada { get; set; } = false;

        public DateTime? FechaCompletada { get; set; }

        public string FechaCreacionTexto =>
            $"Creada: {FechaCreacion:dd/MM/yyyy}";

        public string FechaVencimientoTexto =>
            FechaVencimiento.HasValue
                ? $"Vence: {FechaVencimiento.Value:dd/MM/yyyy}"
                : "";

        public string FechaCompletadaTexto =>
            FechaCompletada.HasValue
                ? $"Completada: {FechaCompletada.Value:dd/MM/yyyy}"
                : "";
    }
}
