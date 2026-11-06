-- Run against the Azure SQL database used by AgriStream before testing.
IF OBJECT_ID('dbo.UserAccounts','U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAccounts (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserAccounts PRIMARY KEY,
        Username nvarchar(64) NOT NULL CONSTRAINT UQ_UserAccounts_Username UNIQUE,
        PasswordHash nvarchar(500) NOT NULL,
        Role nvarchar(20) NOT NULL CONSTRAINT CK_UserAccounts_Role CHECK (Role IN ('Admin','Agronomist','Farmer')),
        CreatedUtc datetime2 NOT NULL CONSTRAINT DF_UserAccounts_CreatedUtc DEFAULT SYSUTCDATETIME()
    );
END;
IF OBJECT_ID('dbo.DeviceRegistry','U') IS NULL
BEGIN
    CREATE TABLE dbo.DeviceRegistry (
        DeviceId nvarchar(100) NOT NULL CONSTRAINT PK_DeviceRegistry PRIMARY KEY,
        FarmId nvarchar(100) NOT NULL,
        OperationalStatus nvarchar(40) NOT NULL,
        LastUpdatedUtc datetime2 NOT NULL
    );
END;
SELECT TABLE_SCHEMA,TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('UserAccounts','DeviceRegistry');
