using AgriStream.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AgriStream.Web.Services;
public sealed class SqlAccountStore(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Sql") ?? "";
    private static readonly PasswordHasher<UserAccount> Hasher = new();
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);
    public async Task InitializeAsync()
    {
        if (!IsConfigured) return;
        await using var connection = new SqlConnection(_connectionString); await connection.OpenAsync();
        var sql = "IF OBJECT_ID('dbo.UserAccounts','U') IS NULL BEGIN CREATE TABLE dbo.UserAccounts (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Username nvarchar(64) NOT NULL UNIQUE, PasswordHash nvarchar(500) NOT NULL, Role nvarchar(20) NOT NULL CONSTRAINT CK_UserAccounts_Role CHECK (Role IN ('Admin','Agronomist','Farmer')), CreatedUtc datetime2 NOT NULL CONSTRAINT DF_UserAccounts_CreatedUtc DEFAULT SYSUTCDATETIME()); END; IF OBJECT_ID('dbo.DeviceRegistry','U') IS NULL BEGIN CREATE TABLE dbo.DeviceRegistry (DeviceId nvarchar(100) NOT NULL PRIMARY KEY, FarmId nvarchar(100) NOT NULL, OperationalStatus nvarchar(40) NOT NULL, LastUpdatedUtc datetime2 NOT NULL); END;";
        await using var command = new SqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
    }
    public async Task<int> CountAsync()
    {
        await using var c = new SqlConnection(_connectionString); await c.OpenAsync(); await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.UserAccounts",c); return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    public async Task<UserAccount?> FindAsync(string username)
    {
        await using var c = new SqlConnection(_connectionString); await c.OpenAsync(); await using var cmd = new SqlCommand("SELECT Id,Username,PasswordHash,Role FROM dbo.UserAccounts WHERE Username=@u",c); cmd.Parameters.Add("@u",SqlDbType.NVarChar,64).Value=username.Trim();
        await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync() ? new UserAccount { Id=r.GetInt32(0),Username=r.GetString(1),PasswordHash=r.GetString(2),Role=r.GetString(3) } : null;
    }
    public bool Verify(UserAccount account,string password) => Hasher.VerifyHashedPassword(account,account.PasswordHash,password) != PasswordVerificationResult.Failed;
    public async Task CreateAsync(string username,string password,string role)
    {
        if (role is not ("Admin" or "Agronomist" or "Farmer")) throw new ArgumentException("Choose Admin, Agronomist or Farmer.");
        var account=new UserAccount { Username=username.Trim(),Role=role }; account.PasswordHash=Hasher.HashPassword(account,password);
        await using var c=new SqlConnection(_connectionString); await c.OpenAsync(); await using var cmd=new SqlCommand("INSERT INTO dbo.UserAccounts(Username,PasswordHash,Role) VALUES(@u,@p,@r)",c);
        cmd.Parameters.Add("@u",SqlDbType.NVarChar,64).Value=account.Username; cmd.Parameters.Add("@p",SqlDbType.NVarChar,500).Value=account.PasswordHash; cmd.Parameters.Add("@r",SqlDbType.NVarChar,20).Value=role; await cmd.ExecuteNonQueryAsync();
    }
}
