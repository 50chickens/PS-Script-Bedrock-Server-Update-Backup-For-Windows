# Minecraft Management Service

A Windows Service built with .NET 10 that automatically manages the Minecraft Bedrock server lifecycle.

┌─────────────────────────────────────────────────────────┐
│  Windows Background Service Starts                      │
└──────────────────────┬──────────────────────────────────┘
                       │
        ┌──────────────▼────────────────┐
        │ UpdateMineCraftOnServiceStart?
        │ (Config option check)         │
        └──────────────┬────────────────┘
                       │
        ┌──────────────▼──────────────────────────────────┐
        │ Check for Updates                               │
        │ (MineCraftUpdateService.NewVersionIsAvailable)  │
        └──────────────┬──────────────────────────────────┘
                       │
        ┌──────────────▼──────────────┐
        │ Update Available?           │
        │ (Compare current vs latest) │
        └──┬─────────────────────┬────┘
           │ YES                 │ NO
           │                     │
    ┌──────▼──────────────────────────────────────┐
    │        Apply Update Process                 │
    │ (MinecraftServerPatchService)               │
    │                                             │
    │ 1. Stop running server gracefully           │
    │ 2. Download new version to temp file        │
    │ 3. Create full backup of current install    │
    │ 4. Extract new version files                │
    │ 5. Clean up temp download                   │
    └──────┬──────────────────────────────────────┘
           │                    │
           └─────────┬──────────┘
                     │
          ┌──────────▼──────────┐
          │ Update Complete     │
          │ or Skip (No Update) │
          └──────────┬──────────┘
                     │
        ┌────────────▼────────────────┐
        │ Server Lifecycle Service    │
        │ (Preflight checks + Start)  │
        │ Check for existing processes│
        │ Verify ports available      │
        │ Start server with delay     │
        └────────────┬────────────────┘
                     │
        ┌────────────▼────────────────────────────────────┐
        │       Server Monitoring Service Loop            │
        │  (MineCraftServerService + Update Orchestration)│
        │                                                 │
        │  Every MonitoringIntervalSeconds (default 30s): │
        │  • Get current server status                    │
        │  • Check if UpdateCheckInterval elapsed         │
        │                                                 │
        │  Every UpdateCheckIntervalSeconds (default 1h): │
        │  • Check for new version available              │
        │  • If found: trigger ApplyUpdateAsync()         │
        │    (Server stops → Backup → Download → Install) │
        │  • Log results and continue monitoring          │
        │                                                 │
        │  Loop until service shutdown requested          │
        └─────────────────────────────────────────────────┘