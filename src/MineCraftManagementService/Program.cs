using Microsoft.Extensions.Hosting;
using MineCraftManagementService;
using MineCraftManagementService.Logging;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var log = LogManager.GetLogger<Program>();

        try
        {
            using var host = ContainerBuilder.Build(args);
            await host.RunAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when the service is stopped via Windows Service Management or Ctrl+C
            log.Info("MineCraft Management Service stopped");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Application exited with an error: {ex.GetType().Name}");
            Environment.Exit(1);
        }
        finally
        {
            NLog.LogManager.Shutdown();
        }
    }
}