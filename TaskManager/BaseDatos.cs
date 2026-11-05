using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TaskManager
{
    public static class BaseDatos
    {
        private static readonly string RutaBaseDatos =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "TaskManager",
                "taskmanager.db"
            );

        private static string CadenaConexion =>
            $"Data Source={RutaBaseDatos}";

        // CREAR BASE DE DATOS Y TABLA
        public static void Inicializar()
        {
            string? directorio = Path.GetDirectoryName(RutaBaseDatos);

            if (directorio != null)
                Directory.CreateDirectory(directorio);

            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                CREATE TABLE IF NOT EXISTS Tareas
                (
                    Id TEXT PRIMARY KEY,
                    Titulo TEXT NOT NULL,
                    Descripcion TEXT NOT NULL DEFAULT '',
                    FechaCreacion TEXT NOT NULL,
                    FechaVencimiento TEXT NULL,
                    Completada INTEGER NOT NULL DEFAULT 0,
                    FechaCompletada TEXT NULL
                );
            ";

            comando.ExecuteNonQuery();

            using var comandoConfiguracion = conexion.CreateCommand();

            comandoConfiguracion.CommandText = @"
                CREATE TABLE IF NOT EXISTS Configuracion
                (
                    Id INTEGER PRIMARY KEY,
                    NotificacionesActivas INTEGER NOT NULL DEFAULT 1,
                    IntervaloMinutos INTEGER NOT NULL DEFAULT 30
                );

                INSERT OR IGNORE INTO Configuracion
                (
                    Id,
                    NotificacionesActivas,
                    IntervaloMinutos
                )
                VALUES (1, 1, 30);
            ";

            comandoConfiguracion.ExecuteNonQuery();
        }

        // OBTENER TAREAS
        public static List<Tarea> ObtenerTareas(bool completadas)
        {
            var tareas = new List<Tarea>();

            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = completadas
                ? @"
                    SELECT *
                    FROM Tareas
                    WHERE Completada = 1
                    ORDER BY FechaCompletada DESC;
                  "
                : @"
                    SELECT *
                    FROM Tareas
                    WHERE Completada = 0
                    ORDER BY FechaCreacion ASC;
                  ";

            using var lector = comando.ExecuteReader();

            while (lector.Read())
            {
                var tarea = new Tarea
                {
                    Id = Guid.Parse(lector.GetString(0)),
                    Titulo = lector.GetString(1),
                    Descripcion = lector.GetString(2),
                    FechaCreacion = DateTime.Parse(
                        lector.GetString(3),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind
                    ),
                    FechaVencimiento = lector.IsDBNull(4)
                        ? null
                        : DateTime.Parse(
                            lector.GetString(4),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind
                        ),
                    Completada = lector.GetInt32(5) == 1,
                    FechaCompletada = lector.IsDBNull(6)
                        ? null
                        : DateTime.Parse(
                            lector.GetString(6),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind
                        )
                };

                tareas.Add(tarea);
            }

            return tareas;
        }

        // GUARDAR NUEVA TAREA
        public static void GuardarTarea(Tarea tarea)
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                INSERT INTO Tareas
                (
                    Id,
                    Titulo,
                    Descripcion,
                    FechaCreacion,
                    FechaVencimiento,
                    Completada,
                    FechaCompletada
                )
                VALUES
                (
                    $id,
                    $titulo,
                    $descripcion,
                    $fechaCreacion,
                    $fechaVencimiento,
                    $completada,
                    $fechaCompletada
                );
            ";

            comando.Parameters.AddWithValue("$id", tarea.Id.ToString());
            comando.Parameters.AddWithValue("$titulo", tarea.Titulo);
            comando.Parameters.AddWithValue("$descripcion", tarea.Descripcion);
            comando.Parameters.AddWithValue("$fechaCreacion", tarea.FechaCreacion.ToString("O"));

            comando.Parameters.AddWithValue(
                "$fechaVencimiento",
                tarea.FechaVencimiento.HasValue
                    ? tarea.FechaVencimiento.Value.ToString("O")
                    : (object)DBNull.Value);

            comando.Parameters.AddWithValue(
                "$completada", tarea.Completada ? 1 : 0);

            comando.Parameters.AddWithValue(
                "$fechaCompletada",
                tarea.FechaCompletada.HasValue
                    ? tarea.FechaCompletada.Value.ToString("O")
                    : (object)DBNull.Value);

            comando.ExecuteNonQuery();
        }

        // COMPLETAR TAREA
        public static void MarcarComoCompletada(Guid id)
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                UPDATE Tareas
                SET
                    Completada = 1,
                    FechaCompletada = $fecha
                WHERE Id = $id;
            ";

            comando.Parameters.AddWithValue("$id", id.ToString());
            comando.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("O"));

            int filas = comando.ExecuteNonQuery();

            if (filas == 0)
                throw new Exception("No se encontró la tarea que se quería completar.");
        }

        // ACTUALIZAR TAREA
        public static void ActualizarTarea(Tarea tarea)
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                UPDATE Tareas
                SET
                    Titulo = $titulo,
                    Descripcion = $descripcion,
                    FechaVencimiento = $fechaVencimiento
                WHERE Id = $id;
            ";

            comando.Parameters.AddWithValue("$id", tarea.Id.ToString());
            comando.Parameters.AddWithValue("$titulo", tarea.Titulo);
            comando.Parameters.AddWithValue("$descripcion", tarea.Descripcion);

            comando.Parameters.AddWithValue(
                "$fechaVencimiento",
                tarea.FechaVencimiento.HasValue
                    ? tarea.FechaVencimiento.Value.ToString("O")
                    : (object)DBNull.Value);

            int filas = comando.ExecuteNonQuery();

            if (filas == 0)
                throw new Exception("No se encontró la tarea que se quería actualizar.");
        }

        // ELIMINAR UNA TAREA
        public static void EliminarTarea(Guid id)
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                DELETE FROM Tareas
                WHERE Id = $id;
            ";

            comando.Parameters.AddWithValue("$id", id.ToString());

            int filas = comando.ExecuteNonQuery();

            if (filas == 0)
                throw new Exception("No se encontró la tarea que se quería eliminar.");
        }

        // ELIMINAR TODAS LAS TAREAS COMPLETADAS
        public static void EliminarTodasLasCompletadas()
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                DELETE FROM Tareas
                WHERE Completada = 1;
            ";

            comando.ExecuteNonQuery();
        }

        // OBTENER CONFIGURACIÓN
        public static (bool NotificacionesActivas, int IntervaloMinutos)
            ObtenerConfiguracion()
        {
            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                SELECT NotificacionesActivas, IntervaloMinutos
                FROM Configuracion
                WHERE Id = 1;
            ";

            using var lector = comando.ExecuteReader();

            if (!lector.Read())
                return (true, 30);

            return (
                lector.GetInt32(0) == 1,
                lector.GetInt32(1)
            );
        }

        // GUARDAR CONFIGURACIÓN
        public static void GuardarConfiguracion(
            bool notificacionesActivas,
            int intervaloMinutos)
        {
            if (intervaloMinutos != 15 &&
                intervaloMinutos != 30 &&
                intervaloMinutos != 45 &&
                intervaloMinutos != 60)
            {
                throw new ArgumentException(
                    "El intervalo seleccionado no es válido.");
            }

            using var conexion = new SqliteConnection(CadenaConexion);

            conexion.Open();

            using var comando = conexion.CreateCommand();

            comando.CommandText = @"
                UPDATE Configuracion
                SET
                    NotificacionesActivas = $activas,
                    IntervaloMinutos = $intervalo
                WHERE Id = 1;
            ";

            comando.Parameters.AddWithValue(
                "$activas",
                notificacionesActivas ? 1 : 0);

            comando.Parameters.AddWithValue(
                "$intervalo",
                intervaloMinutos);

            comando.ExecuteNonQuery();
        }
    }
}
