using System.ComponentModel.DataAnnotations;
namespace AgriStream.Web.Models;
public sealed class LoginModel { [Required] public string Username { get; set; } = ""; [Required,DataType(DataType.Password)] public string Password { get; set; } = ""; }
public sealed class SetupModel { [Required, StringLength(64,MinimumLength=3)] public string Username { get; set; } = ""; [Required,DataType(DataType.Password),StringLength(100,MinimumLength=12)] public string Password { get; set; } = ""; }
public sealed class CreateUserModel { [Required,StringLength(64,MinimumLength=3)] public string Username { get; set; } = ""; [Required,DataType(DataType.Password),StringLength(100,MinimumLength=12)] public string Password { get; set; } = ""; [Required] public string Role { get; set; } = "Farmer"; }
public sealed class UserAccount { public int Id { get; set; } public string Username { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string Role { get; set; } = "Farmer"; }
