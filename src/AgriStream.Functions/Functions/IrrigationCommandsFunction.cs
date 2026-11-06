using System.Net;
using AgriStream.Functions.Models;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions.Functions;

public sealed class IrrigationCommandsFunction(IConfiguration config, ILogger<IrrigationCommandsFunction> logger)
{
    private QueueClient Client()
    {
        var connection = config["Azure:StorageConnection"];
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set Azure:StorageConnection in Function App Configuration.");
        return new QueueClient(connection, config["Azure:IrrigationQueue"] ?? "irrigation-commands");
    }

    [Function("IrrigationCommands")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation/commands")] HttpRequestData req,
        CancellationToken ct)
    {
        IrrigationCommand? command;
        try
        {
            command = await req.ReadFromJsonAsync<IrrigationCommand>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid irrigation command payload.");
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "Invalid request payload." }, ct);
            return bad;
        }

        if (command is null || string.IsNullOrWhiteSpace(command.FarmId) || string.IsNullOrWhiteSpace(command.DeviceId))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "FarmId and DeviceId are required." }, ct);
            return bad;
        }

        try
        {
            var queue = Client();
            await queue.CreateIfNotExistsAsync(cancellationToken: ct);
            await queue.SendMessageAsync(System.Text.Json.JsonSerializer.Serialize(command), ct);

            var ok = req.CreateResponse(HttpStatusCode.Accepted);
            await ok.WriteAsJsonAsync(command, ct);
            return ok;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue irrigation command {CommandId}", command.CommandId);
            var error = req.CreateResponse(HttpStatusCode.InternalServerError);
            await error.WriteAsJsonAsync(new { error = ex.Message }, ct);
            return error;
        }
    }
}