namespace MineCraftManagementService.Enums;

public enum ServerShutDownMode
{
    WindowsServiceShutdown, //we need to prevent restart during Windows service shutdown.
    DenyRestart, //we should never restart after this.
    AllowRestart //normal operation
}