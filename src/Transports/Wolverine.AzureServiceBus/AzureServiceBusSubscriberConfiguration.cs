using Wolverine.AzureServiceBus.Internal;
using Wolverine.Configuration;

namespace Wolverine.AzureServiceBus;

public class AzureServiceBusSubscriberConfiguration : SubscriberConfiguration<AzureServiceBusSubscriberConfiguration,
    AzureServiceBusEndpoint>
{
    public AzureServiceBusSubscriberConfiguration(AzureServiceBusEndpoint queue) : base(queue)
    {
    }
}