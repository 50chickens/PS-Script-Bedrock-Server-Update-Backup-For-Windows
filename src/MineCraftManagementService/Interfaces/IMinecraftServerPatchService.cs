namespace MineCraftManagementService.Interfaces;

public interface IMinecraftServerPatchService
{
    Task ApplyUpdateAsync(string version, CancellationToken cancellationToken);
}
