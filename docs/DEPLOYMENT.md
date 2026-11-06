# Azure setup and evidence checklist

## 1. Create Azure resources

Create a resource group and, in the same region, create:

1. **Storage account** (general-purpose v2, Standard LRS). Keep anonymous Blob access disabled. It will hold the `drone-images` container, `telemetry` and `processingaudit` tables, and `irrigation-commands` queue.
2. **Azure SQL logical server and SQL Database** named `AgriStreamDb`. Allow the Function App and Web App to reach SQL (for a classroom lab, enable the SQL server's Azure-services firewall rule; use a narrower network rule or private endpoint for production). Run `database/schema.sql` in the database.
3. **Function App** using Functions 4.x, .NET 8 isolated worker, and a hosting plan that supports your subscription. Set its required app settings from section 3, then publish `src/AgriStream.Functions` from Visual Studio.
4. **App Service Plan and Web App** for ASP.NET Core 8. Choose a tier that is available in your subscription. Publish `src/AgriStream.Web` to the Web App.
5. **Application Insights** for the Web App and Function App. Use Azure Monitor metrics and Application Insights to capture request rate, response time, failures, CPU and memory during Task E.

Use unique resource names. Do not put connection strings or Function keys in source files or screenshots.

## 2. Create Azure SQL tables

In the Azure portal, open the SQL Database query editor or connect with SQL Server Management Studio. Run `database/schema.sql`. Confirm `dbo.UserAccounts` and `dbo.DeviceRegistry` are listed. The MVC application also creates these tables at startup when its SQL connection is set, but executing the script makes the schema visible as Task B evidence.

The initial administrator is set up at `/Account/Setup` after deployment, but only while `UserAccounts` is empty. Use a strong password of at least 12 characters. Sign in as that admin and create test accounts with Farmer and Agronomist roles.

## 3. Configure application settings

Add these settings in **Web App → Settings → Environment variables → App settings**. Save and restart after adding them. Use the storage account connection string and Function key from the portal; keep them secret.

| Web App setting | Value |
|---|---|
| `ConnectionStrings__Sql` | SQL connection string, with `Initial Catalog=AgriStreamDb` |
| `Azure__StorageConnection` | Storage account connection string |
| `Azure__FunctionsBaseUrl` | Function App base URL, e.g. `https://<function-app>.azurewebsites.net` |
| `Azure__FunctionsKey` | Function key used by the MVC server for its GET and POST calls |
| `Azure__SensorApiKey` | A new long random secret for the load-test telemetry endpoint |
| `Azure__TelemetryTable` | `telemetry` |
| `Azure__AuditTable` | `processingaudit` |
| `Azure__BlobContainer` | `drone-images` |

Add these settings in **Function App → Settings → Environment variables → App settings**:

| Function App setting | Value |
|---|---|
| `AzureWebJobsStorage` | Same storage account connection string; required by the Functions host |
| `StorageConnection` | Same storage account connection string; used by bindings and SDK clients |
| `ConnectionStrings__Sql` | SQL connection string, with `Initial Catalog=AgriStreamDb` |
| `TelemetryTable` | `telemetry` |
| `AuditTable` | `processingaudit` |
| `IrrigationQueue` | `irrigation-commands` |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `FUNCTIONS_EXTENSION_VERSION` | `~4` |

The HTTP Functions use Function-level authorization. In Function App → App keys, copy the **default host key** (or a dedicated host key) into the Web App's `Azure__FunctionsKey` setting.

## 4. Publish and test the application

In Visual Studio, right-click `AgriStream.Functions` → **Publish** → Azure → Azure Function App (Windows or Linux, .NET 8 isolated). Publish `AgriStream.Web` similarly to the App Service Web App. Confirm the Function App Overview shows all four functions. Open the Web App URL, create the first administrator, sign in, then create Farmer/Agronomist accounts.

Perform these checks and capture screenshots:

1. **Task A:** App Service Overview with app name/status and the running MVC site URL.
2. **Task B:** SQL Database overview plus query result showing `UserAccounts` and `DeviceRegistry`; sign-in and role claim shown in the MVC header. Do not show passwords or connection strings.
3. **Task C:** Upload an image and add a sensor reading; show the private Blob and Table entities in Storage browser.
4. **Task D:** Function App page showing exactly four functions. Upload an image to trigger `DroneImageAudit`; call GET `/api/telemetry/latest`; submit an irrigation command in the site (POST); show the queue message processed and device status row updated in SQL.
5. **Task E:** Save load-test console output, CSV, and Azure Monitor/Application Insights screenshots before, during and after the run. Report actual values; do not invent measurements.

## 5. Run Task E load tests

Install Python 3 on your computer. From the solution folder:

```powershell
python .\loadtest\load_test.py https://<your-web-app>.azurewebsites.net --mode page --requests 1000 --concurrency 50 --csv page-results.csv
python .\loadtest\load_test.py https://<your-web-app>.azurewebsites.net --mode telemetry --api-key "<Azure__SensorApiKey>" --requests 1000 --concurrency 50 --csv telemetry-results.csv
```

The first run measures concurrent MVC sign-in page requests. The second sends 1,000 synthetic sensor readings through the MVC App Service into Azure Table Storage. Start with 200 requests and concurrency 10 if your plan is small; increase gradually. Record the App Service plan/tier, test duration, request count, concurrency, successful/error counts, throughput, p50/p95/p99 latency, and Azure Monitor CPU/memory. Stop the run if the site returns sustained errors or resource limits are reached. The script does not create Azure resources or automatically scale them.

## Notes

- `local.settings.example.json` is a template. Copy it to `local.settings.json` only for local Functions development; the actual secret-bearing file is ignored from source control.
- For local MVC login over HTTP, the cookie allows same-request security; deployed production uses HTTPS-only hosting.
- The demo sensor-ingestion endpoint requires `X-AgriStream-Key`; set a long random key in App Service settings. The key protects the test endpoint and is not a replacement for a production device identity system.
- Azure resources can incur charges, especially SQL, App Service and Application Insights. Review the selected tiers and usage in your subscription before creating them.
