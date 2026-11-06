using System.Data;
using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

public sealed class AgriStreamFunctions(IConfiguration config,ILogger<AgriStreamFunctions> logger)
{
    private string StorageConnection => config["StorageConnection"] ?? config["AzureWebJobsStorage"] ?? throw new InvalidOperationException("StorageConnection is missing.");

    // 1 of exactly 4 functions: records every uploaded image in Azure Table Storage.
    [Function("DroneImageAudit")]
    public async Task OnImageUploaded([BlobTrigger("drone-images/{name}",Connection="StorageConnection")] Stream image,string name,CancellationToken ct)
    {
        var audit=new TableClient(StorageConnection,config["AuditTable"]??"processingaudit"); await audit.CreateIfNotExistsAsync(ct);
        var row=new TableEntity("drone-image",$"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}")
        { ["BlobName"]=name,["SizeBytes"]=image.Length,["Status"]="Uploaded",["ProcessedAt"]=DateTimeOffset.UtcNow };
        await audit.AddEntityAsync(row,ct); logger.LogInformation("Recorded uploaded drone image {BlobName} ({Length} bytes)",name,image.Length);
    }

    // 2 of exactly 4 functions: returns the newest sensor readings from Azure Table Storage.
    [Function("GetLatestTelemetry")]
    public async Task<HttpResponseData> GetLatestTelemetry([HttpTrigger(AuthorizationLevel.Function,"get",Route="telemetry/latest")] HttpRequestData request,CancellationToken ct)
    {
        var farmPair=request.Url.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)
            .Select(part=>part.Split('=',2))
            .FirstOrDefault(pair=>Uri.UnescapeDataString(pair[0].Replace('+',' ')).Equals("farmId",StringComparison.OrdinalIgnoreCase));
        var farmId=farmPair is { Length: > 1 } ? Uri.UnescapeDataString(farmPair[1].Replace('+',' ')) : null;
        if(string.IsNullOrWhiteSpace(farmId)){var bad=request.CreateResponse(HttpStatusCode.BadRequest);await bad.WriteStringAsync("Supply farmId to query one telemetry partition.",ct);return bad;}
        var table=new TableClient(StorageConnection,config["TelemetryTable"]??"telemetry"); await table.CreateIfNotExistsAsync(ct);
        var items=new List<TelemetryDto>();
        var safeFarmId=farmId.Replace("'","''");
        await foreach(var entity in table.QueryAsync<TableEntity>(filter:$"PartitionKey eq '{safeFarmId}'",maxPerPage:100,cancellationToken:ct))
        {
            items.Add(new(entity.PartitionKey,entity.GetString("DeviceId")??"unknown",entity.GetDouble("SoilMoisture")??0,entity.GetDouble("TemperatureC")??0,entity.GetDouble("HumidityPercent")??0,entity.GetDateTimeOffset("RecordedAt")??entity.Timestamp??DateTimeOffset.MinValue));
            if(items.Count==100)break;
        }
        var response=request.CreateResponse(HttpStatusCode.OK); await response.WriteAsJsonAsync(items,ct); return response;
    }

    // 3 of exactly 4 functions: accepts an irrigation command and sends it to Azure Queue Storage.
    [Function("QueueIrrigationCommand")]
    public async Task<HttpResponseData> QueueIrrigationCommand([HttpTrigger(AuthorizationLevel.Function,"post",Route="irrigation/commands")] HttpRequestData request,CancellationToken ct)
    {
        IrrigationCommand? command;
        try { command=await request.ReadFromJsonAsync<IrrigationCommand>(cancellationToken:ct); }
        catch(JsonException) { command=null; }
        if(command is null||string.IsNullOrWhiteSpace(command.DeviceId)||string.IsNullOrWhiteSpace(command.FarmId)||command.DurationMinutes is <1 or >360)
        { var bad=request.CreateResponse(HttpStatusCode.BadRequest); await bad.WriteStringAsync("Provide FarmId, DeviceId and DurationMinutes (1-360).",ct); return bad; }
        var queue=new QueueClient(StorageConnection,config["IrrigationQueue"]??"irrigation-commands",new QueueClientOptions { MessageEncoding=QueueMessageEncoding.Base64 }); await queue.CreateIfNotExistsAsync(cancellationToken:ct);
        await queue.SendMessageAsync(JsonSerializer.Serialize(command),cancellationToken:ct);
        var response=request.CreateResponse(HttpStatusCode.Accepted); await response.WriteAsJsonAsync(new { command.CommandId, status="Queued" },ct); return response;
    }

    // 4 of exactly 4 functions: processes the command and upserts device status in Azure SQL.
    [Function("ProcessIrrigationCommand")]
    public async Task ProcessIrrigationCommand([QueueTrigger("%IrrigationQueue%",Connection="StorageConnection")] string message,CancellationToken ct)
    {
        var command=JsonSerializer.Deserialize<IrrigationCommand>(message)??throw new InvalidOperationException("Queue message was not a valid irrigation command.");
        var connectionString=config.GetConnectionString("Sql")??config["ConnectionStrings__Sql"]??throw new InvalidOperationException("ConnectionStrings__Sql is missing.");
        await using var connection=new SqlConnection(connectionString); await connection.OpenAsync(ct);
        const string sql="""
            MERGE dbo.DeviceRegistry AS target
            USING (SELECT @DeviceId AS DeviceId) AS source ON target.DeviceId=source.DeviceId
            WHEN MATCHED THEN UPDATE SET FarmId=@FarmId, OperationalStatus=@Status, LastUpdatedUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT(DeviceId,FarmId,OperationalStatus,LastUpdatedUtc) VALUES(@DeviceId,@FarmId,@Status,SYSUTCDATETIME());
            """;
        await using var dbCommand=new SqlCommand(sql,connection); dbCommand.Parameters.Add("@DeviceId",SqlDbType.NVarChar,100).Value=command.DeviceId; dbCommand.Parameters.Add("@FarmId",SqlDbType.NVarChar,100).Value=command.FarmId; dbCommand.Parameters.Add("@Status",SqlDbType.NVarChar,40).Value=$"Irrigating {command.DurationMinutes} min"; await dbCommand.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Processed irrigation command {CommandId} for device {DeviceId}",command.CommandId,command.DeviceId);
    }
}

public sealed record TelemetryDto(string FarmId,string DeviceId,double SoilMoisture,double TemperatureC,double HumidityPercent,DateTimeOffset RecordedAt);
public sealed record IrrigationCommand(string CommandId,string FarmId,string DeviceId,int DurationMinutes,DateTimeOffset RequestedAt);
