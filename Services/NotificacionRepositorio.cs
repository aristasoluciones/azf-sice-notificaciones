using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace sice.Functions.Notificaciones.Services
{
    /// <summary>
    /// Acceso a la base de datos por stored procedures, igual que el panel.
    /// Se conecta con un usuario restringido que solo puede ejecutar estos dos
    /// procedimientos: no tiene acceso al resto de la información.
    /// </summary>
    public class NotificacionRepositorio : INotificacionRepositorio
    {
        private readonly string _connectionString;

        public NotificacionRepositorio(IConfiguration config)
        {
            // Por configuración de la aplicación, no por variable de entorno: en
            // Linux los nombres con dos puntos llegan con otro formato.
            _connectionString = config["Db:ConnectionString"]!;
        }

        public async Task<string?> GetStatusAsync(int idNotificacion)
        {
            using IDbConnection cnn = new NpgsqlConnection(_connectionString);

            var _params = new DynamicParameters();
            _params.Add("@_id_notificacion", idNotificacion);

            string json = await cnn.QueryFirstAsync<string>(
                "core.notificacion_externa_status_get", _params, commandType: CommandType.StoredProcedure);

            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetProperty("status").GetString() != "OK") { return null; }

            return doc.RootElement.GetProperty("data").GetProperty("status_notificacion").GetString();
        }

        public async Task RegistrarEnvioAsync(int idNotificacion, string status)
        {
            using IDbConnection cnn = new NpgsqlConnection(_connectionString);

            var _params = new DynamicParameters();
            _params.Add("@_id_notificacion", idNotificacion);
            _params.Add("@_status", status);

            await cnn.QueryFirstAsync<string>(
                "core.notificacion_externa_upd_envio", _params, commandType: CommandType.StoredProcedure);
        }
    }
}
