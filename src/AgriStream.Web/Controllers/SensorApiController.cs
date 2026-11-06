using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace AgriStream.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/sensors/telemetry")]
public sealed class SensorApiController(IConfiguration config,TelemetryTableService telemetry):ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody]TelemetryInput reading,[FromHeader(Name="X-AgriStream-Key")]string? suppliedKey,CancellationToken ct)
    {
        var expected=config["Azure:SensorApiKey"];
        if(string.IsNullOrWhiteSpace(expected)||string.IsNullOrEmpty(suppliedKey)||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected),Encoding.UTF8.GetBytes(suppliedKey))) return Unauthorized();
        await telemetry.AddAsync(reading,ct);
        return Accepted(new { status="Stored",reading.DeviceId,receivedAt=DateTimeOffset.UtcNow });
    }
}
