using System.Data;
using AgriStream.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

public sealed class DeviceRegistryQueueFunction(IConfiguration config, ILogger<DeviceRegistryQueueFunction> logger)
{
    [Function("DeviceRegistryQueueWorker")]
    public async Task RunAsync(
        [QueueTrigger("%Azure:IrrigationQueue%", Connection = "Azure:StorageConnection")] string message,
        CancellationToken ct)
    {
        var connectionString = config.GetConnectionString("Sql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("ConnectionStrings:Sql is not configured; skipping device registry update.");
            return;
        }

        IrrigationCommand? command;
        try
        {
            command = System.Text.Json.JsonSerializer.Deserialize<IrrigationCommand>(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize irrigation command message.");
            return;
        }

        if (command is null)
        {
            logger.LogWarning("Irrigation command message deserialized to null.");
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        const string sql = """
            MERGE dbo.DeviceRegistry AS target
            USING (SELECT @DeviceId AS DeviceId, @FarmId AS FarmId, @Status AS OperationalStatus, @Now AS LastUpdatedUtc) AS source
            ON target.DeviceId = source.DeviceId
            WHEN MATCHED THEN
                UPDATE SET FarmId = source.FarmId, OperationalStatus = source.OperationalStatus, LastUpdatedUtc = source.LastUpdatedUtc
            WHEN NOT MATCHED THEN
                INSERT (DeviceId, FarmId, OperationalStatus, LastUpdatedUtc)
                VALUES (source.DeviceId, source.FarmId, source.OperationalStatus, source.LastUpdatedUtc);
            """;
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@DeviceId", SqlDbType.NVarChar, 100).Value = command.DeviceId;
        cmd.Parameters.Add("@FarmId", SqlDbType.NVarChar, 100).Value = command.FarmId;
        cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 40).Value = $"Irrigating ({command.DurationMinutes} min)";
        cmd.Parameters.Add("@Now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
        await cmd.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Device registry updated for {DeviceId} on farm {FarmId}.", command.DeviceId, command.FarmId);
    }
}