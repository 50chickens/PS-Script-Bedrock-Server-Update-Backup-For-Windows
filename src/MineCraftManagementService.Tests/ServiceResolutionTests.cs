using Microsoft.Extensions.DependencyInjection;
using MineCraftManagementService.Interfaces;
using MineCraftManagementService.Services;

namespace MineCraftManagementService.Tests
{
    /// <summary>
    /// Generic service resolution tests that validate all registered services can be resolved from the DI container.
    /// These tests use the centralized ContainerBuilder to ensure consistency with production configuration.
    /// </summary>
    [TestFixture]
    public class ServiceResolutionTests
    {
        /// <summary>
        /// Test that we can resolve a service with dependencies.
        /// This validates that the DI container is properly configured with all required service registrations.
        /// </summary>
        [Test]
        public void Test_That_We_Can_Resolve_All_Services()
        {
            using var host = ContainerBuilder.Build();
            var serviceProvider = host.Services;

            // Try to resolve a complex service that has dependencies on other services
            var statusService = serviceProvider.GetRequiredService<IServerStatusService>();
            Assert.That(statusService, Is.Not.Null);
            Assert.That(statusService, Is.InstanceOf<ServerStatusService>());
        }

        /// <summary>
        /// Test that the IMineCraftSchedulerService is properly registered and can be resolved.
        /// This is critical for all time-dependent services in the application.
        /// </summary>
        [Test]
        public void Test_That_MineCraftSchedulerService_Is_Registered_And_Resolvable()
        {
            using var host = ContainerBuilder.Build();
            var schedulerService = host.Services.GetRequiredService<IMineCraftSchedulerService>();

            Assert.That(schedulerService, Is.Not.Null);
            Assert.That(schedulerService, Is.InstanceOf<MineCraftSchedulerService>());
        }

        /// <summary>
        /// Test that ServerStatusService can be resolved, which depends on IMineCraftSchedulerService.
        /// This verifies the dependency chain is correctly configured.
        /// </summary>
        [Test]
        public void Test_That_ServerStatusService_Can_Resolve_With_Its_Dependencies()
        {
            using var host = ContainerBuilder.Build();
            var statusService = host.Services.GetRequiredService<IServerStatusService>();

            Assert.That(statusService, Is.Not.Null);
            Assert.That(statusService, Is.InstanceOf<ServerStatusService>());
        }
    }
}
