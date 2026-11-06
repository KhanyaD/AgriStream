using System.Net;
using AgriStream.Functions.Models;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

public sealed class TelemetryLatestFunction(IConfiguration config, ILogger<TelemetryLatestFunction> logger)
{
    private TableClient Client()
    {
        var connection = config["Azure:StorageConnection"];
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set Azure:StorageConnection in Function App Configuration.");
        return new TableClient(connection, config["Azure:TelemetryTable"] ?? "telemetry");
    }

    [Function("TelemetryLatest")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/latest")] HttpRequestData req,
        CancellationToken ct)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var farmId = query["farmId"];
        if (string.IsNullOrWhiteSpace(farmId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "farmId query parameter is required." }, ct);
            return bad;
        }

        try
        {
            var table = Client();
            await table.CreateIfNotExistsAsync(cancellationToken: ct);
            var safeFarmId = farmId.Replace("'", "''");
            var records = new List<TelemetryRecord>();
            await foreach (var row in table.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{safeFarmId}'", maxPerPage: 50, cancellationToken: ct))
            {
                records.Add(new(
                    row.PartitionKey,
                    row.GetString("DeviceId") ?? "unknown",
                    row.GetDouble("SoilMoisture") ?? 0,
                    row.GetDouble("TemperatureC") ?? 0,
                    row.GetDouble("HumidityPercent") ?? 0,
                    row.GetDateTimeOffset("RecordedAt") ?? row.Timestamp ?? DateTimeOffset.MinValue));
                if (records.Count >= 50) break;
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(records, ct);
            return ok;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read telemetry for farm {FarmId}", farmId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { error = ex.Message }, ct);
            return error;
        }
    }
}