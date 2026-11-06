using System.ComponentModel.DataAnnotations;
namespace AgriStream.Web.Models;
public sealed class TelemetryInput { [Required] public string DeviceId { get; set; } = ""; [Required] public string FarmId { get; set; } = ""; [Range(-50,100)] public double SoilMoisture { get; set; } [Range(-50,80)] public double TemperatureC { get; set; } [Range(0,100)] public double HumidityPercent { get; set; } }
public sealed class IrrigationInput { [Required] public string DeviceId { get; set; } = ""; [Required] public string FarmId { get; set; } = ""; [Range(1,360)] public int DurationMinutes { get; set; } = 10; }
public sealed record TelemetryRecord(string FarmId,string DeviceId,double SoilMoisture,double TemperatureC,double HumidityPercent,DateTimeOffset RecordedAt);
public sealed record IrrigationCommand(string CommandId,string FarmId,string DeviceId,int DurationMinutes,DateTimeOffset RequestedAt);
