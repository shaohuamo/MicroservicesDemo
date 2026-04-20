using System.Text;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;

namespace ProductsMicroservice.Core.Policies
{
    //redundant with PollyPolicies, just for testing the fallback policy
    public class TestMicroservicePolicies: ITestMicroservicePolicies
    {
        private readonly ILogger<TestMicroservicePolicies> _logger;

        public TestMicroservicePolicies(ILogger<TestMicroservicePolicies> logger)
        {
            _logger = logger;
        }


        public IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy()
        {
            AsyncFallbackPolicy<HttpResponseMessage> policy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .FallbackAsync(async (_) =>
                {
                    _logger.LogWarning("Fallback triggered: The request failed, returning dummy data");

                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new StringContent("false", Encoding.UTF8, "application/json")
                    };

                    return await Task.FromResult(response);
                });

            return policy;
        }
    }
}
