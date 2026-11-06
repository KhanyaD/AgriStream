using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

public sealed class DroneImageAuditFunction(IConfiguration config, ILogger<DroneImageAuditFunction> logger)
{
    [Function("DroneImageAudit")]
    public async Task RunAsync(
        [BlobTrigger("%Azure:BlobContainer%/{name}", Connection = "Azure:StorageConnection")] Stream blob,
        string name,
        CancellationToken ct)
    {
        var connection = config["Azure:StorageConnection"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            logger.LogWarning("Azure:StorageConnection is not configured; skipping audit record for {Name}.", name);
            return;
        }

        var table = new TableClient(connection, config["Azure:AuditTable"] ?? "processingaudit");
        await table.CreateIfNotExistsAsync(ct);

        var entity = new TableEntity(DateTime.UtcNow.ToString("yyyy-MM-dd"), $"{DateTime.UtcNow:HHmmssfff}-{Guid.NewGuid():N}")
        {
            ["BlobName"] = name,
            ["SizeBytes"] = blob.Length,
            ["ProcessedAtUtc"] = DateTimeOffset.UtcNow
        };
        await table.AddEntityAsync(entity, ct);

        logger.LogInformation("Recorded audit entry for uploaded image {Name} ({Size} bytes).", name, blob.Length);
    }
}