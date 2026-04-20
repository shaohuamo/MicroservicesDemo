using Microsoft.AspNetCore.Mvc;
using System;

namespace TestMicroservice.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly IConfiguration _configuration;

        public TestController(IConfiguration configuration, ILogger<TestController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public string Get()
        {
            return $"Hello World From {_configuration["App_Name"]}";
        }

        [HttpDelete("product/{productId}/related-info")]
        public async Task<ActionResult<bool>> DeleteProductRelatedInfoAsync(Guid productId)
        {
            //mocking the deletion of product related info
            var success = await PerformDeletionAsync(productId);

            if (!success)
            {
                _logger.LogWarning("Failed to delete product related info for product {ProductId}", productId);
                return NotFound(false);
            }

            _logger.LogInformation("Successfully deleted product related info for product {ProductId}", productId);
            return Ok(true);
        }

        private async Task<bool> PerformDeletionAsync(Guid productId)
        {
            await Task.Delay(1000);// Simulate some processing time

            var random = new Random();
            return random.Next(2) == 0;// Randomly return true or false to simulate success or failure
        }
    }
}
