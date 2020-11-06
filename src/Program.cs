using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using CommandLine;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.Sql.Fluent.SqlServer.Definition;
using Microsoft.Rest;

namespace GSoft.DiskSnapshotTool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CreateOptions>(args)
                .WithParsedAsync(async options =>
                {
                    var azureCredentials = await GetCredentials(options.TenantId);

                    var azure = Microsoft.Azure.Management.Fluent.Azure
                        .Configure()
                        .Authenticate(azureCredentials)
                        .WithSubscription(options.SubscriptionId);

                    var disk = await azure.Disks.GetByIdAsync(options.ManagedDiskId);
                    var snapshotName = options.GenerateSnapshotName();

                    Console.WriteLine($"Creating snapshot '{snapshotName}'...");

                    var stopwatch = Stopwatch.StartNew();
                    await azure.Snapshots.Define(snapshotName)
                        .WithRegion(disk.Region)
                        .WithExistingResourceGroup(disk.ResourceGroupName)
                        .WithDataFromDisk(disk)
                        .WithSku(SnapshotSkuType.FromStorageAccountType(SnapshotStorageAccountTypes.Parse(options.SnapshotSkuType)))
                        .CreateAsync();
                    stopwatch.Stop();
                    Console.WriteLine($"Done creating snapshot in {stopwatch.Elapsed:g}.");

                    if (options.RetainLimit > 0)
                    {
                        var existingSnapshots = await azure.Snapshots.ListByResourceGroupAsync(options.ResourceGroup);
                        var retainedSnapshots = existingSnapshots
                            .OrderByDescending(x => x.Inner.TimeCreated)
                            .Take(options.RetainLimit);

                        var discardedSnapshotIds = existingSnapshots
                            .Except(retainedSnapshots)
                            .Select(x => x.Id)
                            .ToArray();

                        if (discardedSnapshotIds.Any())
                        {
                            Console.WriteLine(
                                $"Retaining {options.RetainLimit} snapshot(s) and discarding {discardedSnapshotIds.Length} snapshot(s)...");

                            stopwatch.Restart();
                            await azure.Snapshots.DeleteByIdsAsync(discardedSnapshotIds);
                            stopwatch.Stop();
                            Console.WriteLine($"Done discarding snapshot(s) in {stopwatch.Elapsed:g}.");
                        }
                    }
                });
        }

        private static async Task<AzureCredentials> GetCredentials(string tenantId)
        {
            var scopes = new[] {"https://management.azure.com/.default"};

            var defaultAzureCredential = new DefaultAzureCredential();

            var accessToken = await defaultAzureCredential.GetTokenAsync(new TokenRequestContext(scopes));

            var tokenCredentials = new TokenCredentials(accessToken.Token);

            return new AzureCredentials(
                tokenCredentials,
                tokenCredentials,
                tenantId,
                AzureEnvironment.AzureGlobalCloud);
        }
    }

    [Verb("create", HelpText = "Creates a snapshot of the specified managed disk.")]
    internal sealed class CreateOptions
    {
        [Option(shortName: 't', longName: "tenantId", Required = true, HelpText = "Tenant ID against which to authenticate the current Azure credentials.")]
        public string TenantId { get; set; }

        [Option(shortName: 's', longName: "subscriptionId", Required = true, HelpText = "Subscription ID in which the managed disk exists and in which the snapshot will be created.")]
        public string SubscriptionId { get; set; }

        [Option(shortName: 'g', longName: "resourceGroup", Required = true, HelpText = "Resource group in which the managed disk exists and in which the snapshot will be created.")]
        public string ResourceGroup { get; set; }

        [Option(shortName: 'n', longName: "diskName", Required = true, HelpText = "Name of the managed disk from which to take a snapshot.")]
        public string DiskName { get; set; }

        [Option(shortName: 'f', longName: "snapshotNameFormat", Default = "{0}-snapshot-{1:yy-MM-dd.hh.mm.ss}", HelpText = "Defines the name of the snapshot resource.  Default is 'diskName-snapshot-yy-MM-dd.hh.mm.ss'.")]
        public string SnapshotFormat { get; set; }

        [Option(shortName: 'l', longName: "retainLimit", Default = 0, HelpText = "Limits the retained snapshots to specified count.  Default is unlimited (0).")]
        public int RetainLimit { get; set; }


        [Option(shortName: 'k', longName: "skuType",  Default = "Standard_LRS", HelpText = "Snapshot sku type.  Available values are 'Standard_LRS' or 'Premium_LRS'. Default is 'Standard_LRS'.")]
        public string SnapshotSkuType { get; set; }

        public string ManagedDiskId => $"/subscriptions/{this.SubscriptionId}/resourceGroups/{this.ResourceGroup}/providers/Microsoft.Compute/disks/{this.DiskName}";

        public string GenerateSnapshotName() => string.Format(this.SnapshotFormat, this.DiskName, DateTimeOffset.UtcNow);
    }
}