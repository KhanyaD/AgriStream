using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AgriStream.Web.Controllers;
[Authorize] public sealed class HomeController(TelemetryTableService telemetry,DroneBlobService blobs,FunctionsClient functions):Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {const string farmId="farm-001";ViewBag.FarmId=farmId;try{ViewBag.Telemetry=await functions.LatestAsync(farmId,ct);}catch(Exception e){ViewBag.StorageError=e.Message;ViewBag.Telemetry=Array.Empty<TelemetryRecord>();}return View();}
    [Authorize(Roles="Admin,Agronomist,Farmer"),HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTelemetry(TelemetryInput model,CancellationToken ct)
    {if(!ModelState.IsValid){TempData["Error"]="Check the telemetry values.";return RedirectToAction(nameof(Index));}try{await telemetry.AddAsync(model,ct);TempData["Message"]="Telemetry reading saved to Azure Table Storage.";}catch(Exception e){TempData["Error"]=e.Message;}return RedirectToAction(nameof(Index));}
    [Authorize(Roles="Admin,Agronomist,Farmer"),HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile image,CancellationToken ct)
    {if(image is null||image.Length==0){TempData["Error"]="Select an image file.";return RedirectToAction(nameof(Index));}if(image.Length>20*1024*1024){TempData["Error"]="Image must be 20 MB or smaller.";return RedirectToAction(nameof(Index));}try{var name=await blobs.UploadAsync(image,ct);TempData["Message"]=$"Image uploaded as {name}; the Blob-triggered Function will record its audit entry.";}catch(Exception e){TempData["Error"]=e.Message;}return RedirectToAction(nameof(Index));}
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Irrigate(IrrigationInput model,CancellationToken ct)
    {if(!ModelState.IsValid){TempData["Error"]="Check the irrigation command.";return RedirectToAction(nameof(Index));}try{await functions.QueueIrrigationAsync(model,ct);TempData["Message"]="Command queued. The Queue-triggered Function will update the device registry.";}catch(Exception e){TempData["Error"]=e.Message;}return RedirectToAction(nameof(Index));}
    public async Task<IActionResult> Latest(string farmId="farm-001",CancellationToken ct=default){try{return Json(await functions.LatestAsync(farmId,ct));}catch(Exception e){return Problem(e.Message);}}
}
