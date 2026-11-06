# AgriStream IoT — Section A (Tasks A–E)

Standalone ASP.NET Core MVC and Azure Functions solution for the AgriStream brief. It uses Azure App Service, Azure SQL Database, Azure Blob Storage, Azure Table Storage, Azure Queue Storage, and exactly four Azure Functions.

## Project layout

- `src/AgriStream.Web`: MVC dashboard, cookie sign-in with role claims, SQL account store, Blob image uploads, Table telemetry, and a protected-by-key sensor-ingestion endpoint.
- `src/AgriStream.Functions`: four functions: Blob trigger, HTTP GET, HTTP POST, and Queue trigger.
- `database/schema.sql`: SQL tables for user/role management and the device registry.
- `loadtest/load_test.py`: concurrent App Service page and telemetry-ingestion load test; writes per-request CSV results.
- `docs/DEPLOYMENT.md`: Azure setup, deployment, configuration, evidence and test steps.

## Open and run

Use Visual Studio 2022 with the .NET 8 SDK, ASP.NET and web development workload, and Azure development workload. Open `AgriStream.sln`; build both projects. Start `AgriStream.Web` for the MVC site. The Functions project needs a local Storage emulator or an Azure Storage connection, as well as Azure SQL configuration; use `local.settings.example.json` as a template and save your actual local values as `local.settings.json` (do not submit secrets).

## Function inventory

| Function | Trigger | Responsibility |
|---|---|---|
| `DroneImageAudit` | Blob (`drone-images/{name}`) | Records image name, size and processing status in Table Storage. |
| `GetLatestTelemetry` | HTTP GET | Reads latest telemetry records from Table Storage. |
| `QueueIrrigationCommand` | HTTP POST | Validates and writes irrigation messages to Queue Storage. |
| `ProcessIrrigationCommand` | Queue | Processes each irrigation command and upserts device status in Azure SQL. |

Telemetry rows are partitioned by farm and use reverse-time RowKeys so the newest records can be read first within a farm partition. Blob containers, tables and queues are created on first use. The account table stores a salted password hash, never a plaintext password. The first visitor can create the initial administrator only while the SQL account table is empty; an administrator can then create Farmer, Agronomist or Admin accounts.

## Requirements map

| Brief task | Implementation/evidence |
|---|---|
| A — Web hosting | Publish `AgriStream.Web` to Azure App Service. |
| B — Relational user management | `UserAccounts` SQL table, role-based cookie claims and `database/schema.sql`; screenshot SQL database plus working sign-in. |
| C — Cloud storage | Private `drone-images` Blob container and `telemetry` Table Storage table. |
| D — Serverless pipeline | The four trigger types listed above; upload an image, invoke GET/POST, and show Queue-triggered SQL status. |
| E — Load testing | Run `load_test.py` in page mode and telemetry mode; retain CSV, console metrics, App Insights/Azure Monitor graphs and timestamps. |

See `docs/DEPLOYMENT.md` before creating resources. Deployment and load testing must be performed in your Azure subscription; this code does not claim those actions have already happened.
