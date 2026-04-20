using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.ExternalServices.Abstractions;
using System.Diagnostics;
using System.Net.Http.Json;

namespace ProductsMicroservice.Core.HttpClients
{
    public class TestMicroserviceClient: ITestMicroserviceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TestMicroserviceClient> _logger;

        public TestMicroserviceClient(HttpClient httpClient, ILogger<TestMicroserviceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> DeleteProductRelatedInfoByProductIdAsync(Guid productId)
        {
            var activity = Activity.Current;
            activity?.SetTag("test.api.called", true);
            activity?.AddEvent(new("Downstream Call Start: TestMicroservice"));

            try
            {
                HttpResponseMessage response =
                    await _httpClient.DeleteAsync($"/api/test/product/{productId}/related-info");

                activity?.SetTag("test.api.success", response.IsSuccessStatusCode);

                bool isDeleted = await response.Content.ReadFromJsonAsync<bool>();

                _logger.LogInformation("TestApi delete product related info completed , isDeleted: {isDeleted}", isDeleted);
                return isDeleted;
            }
            catch (Exception e)
            {
                activity?.AddException(e);
                _logger.LogError(e, "Downstream call failed for {ProductId}", productId);
                return false;//falback policy
            }
        }
    }
}
