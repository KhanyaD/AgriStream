using AgriStream.Web.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AgriStream.Web.Services;
public sealed class DroneBlobService(IConfiguration config)
{
    public async Task<string> UploadAsync(IFormFile file,CancellationToken ct)
    {
        var connection=config["Azure:StorageConnection"]; if(string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set Azure:StorageConnection in App Service Configuration.");
        var container=new BlobContainerClient(connection,config["Azure:BlobContainer"]??"drone-images"); await container.CreateIfNotExistsAsync(PublicAccessType.None,cancellationToken:ct);
        var safeName=$"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}"; var blob=container.GetBlobClient(safeName);
        await using var stream=file.OpenReadStream(); await blob.UploadAsync(stream,new BlobHttpHeaders { ContentType=string.IsNullOrWhiteSpace(file.ContentType)?"application/octet-stream":file.ContentType },cancellationToken:ct); return safeName;
    }
}
public sealed class TelemetryTableService(IConfiguration config)
{
    private TableClient Client(string? tableName=null)
    {
        var connection=config["Azure:StorageConnection"]; if(string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set Azure:StorageConnection in App Service Configuration.");
        return new TableClient(connection,tableName??config["Azure:TelemetryTable"]??"telemetry");
    }
    public async Task AddAsync(TelemetryInput input,CancellationToken ct)
    {
        var table=Client(); await table.CreateIfNotExistsAsync(ct); var now=DateTimeOffset.UtcNow;
        var reverseTicks=DateTime.MaxValue.Ticks-now.UtcDateTime.Ticks;
        var row=new TableEntity(input.FarmId.Trim(),$"{reverseTicks:D19}-{Guid.NewGuid():N}") { ["DeviceId"]=input.DeviceId.Trim(),["SoilMoisture"]=input.SoilMoisture,["TemperatureC"]=input.TemperatureC,["HumidityPercent"]=input.HumidityPercent,["RecordedAt"]=now };
        await table.AddEntityAsync(row,ct);
    }
    public async Task<IReadOnlyList<TelemetryRecord>> LatestAsync(string farmId,int take=50,CancellationToken ct=default)
    {
        var table=Client(); await table.CreateIfNotExistsAsync(ct); var records=new List<TelemetryRecord>(); var safeFarmId=farmId.Replace("'","''"); var limit=Math.Clamp(take,1,100);
        await foreach(var row in table.QueryAsync<TableEntity>(filter:$"PartitionKey eq '{safeFarmId}'",maxPerPage:limit,cancellationToken:ct))
        { records.Add(new(row.PartitionKey,row.GetString("DeviceId")??"unknown",row.GetDouble("SoilMoisture")??0,row.GetDouble("TemperatureC")??0,row.GetDouble("HumidityPercent")??0,row.GetDateTimeOffset("RecordedAt")??row.Timestamp??DateTimeOffset.MinValue)); if(records.Count>=limit)break; }
        return records;
    }
}
public sealed class FunctionsClient(IConfiguration config,IHttpClientFactory factory)
{
    private async Task<HttpClient> CreateAsync()
    {
        var baseUrl=config["Azure:FunctionsBaseUrl"]; if(string.IsNullOrWhiteSpace(baseUrl)) throw new InvalidOperationException("Set Azure:FunctionsBaseUrl in App Service Configuration.");
        var client=factory.CreateClient("Functions"); client.BaseAddress=new Uri(baseUrl.TrimEnd('/')+"/"); var key=config["Azure:FunctionsKey"]; if(!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-functions-key",key); return client;
    }
    public async Task<IReadOnlyList<TelemetryRecord>> LatestAsync(string farmId,CancellationToken ct)
    { using var client=await CreateAsync(); using var response=await client.GetAsync("api/telemetry/latest?farmId="+Uri.EscapeDataString(farmId),ct); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<List<TelemetryRecord>>(cancellationToken:ct)??[]; }
    public async Task QueueIrrigationAsync(IrrigationInput input,CancellationToken ct)
    { using var client=await CreateAsync(); using var response=await client.PostAsJsonAsync("api/irrigation/commands",new IrrigationCommand(Guid.NewGuid().ToString("N"),input.FarmId.Trim(),input.DeviceId.Trim(),input.DurationMinutes,DateTimeOffset.UtcNow),ct); response.EnsureSuccessStatusCode(); }
}
