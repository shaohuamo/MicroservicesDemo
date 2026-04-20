using Consul;
using Ocelot.Logging;
using Ocelot.Provider.Consul.Interfaces;
using Ocelot.Provider.Consul;

namespace ApiGateway.ConsulServiceBuilder
{
    public class MyConsulServiceBuilder : DefaultConsulServiceBuilder
    {
        public MyConsulServiceBuilder(IHttpContextAccessor contextAccessor, 
            IConsulClientFactory clientFactory, IOcelotLoggerFactory loggerFactory)
            : base(contextAccessor, clientFactory, loggerFactory) { }

        // Use the agent service IP address as the downstream hostname
        protected override string GetDownstreamHost(ServiceEntry entry, Node node)
            => entry.Service.Address;
    }
}
