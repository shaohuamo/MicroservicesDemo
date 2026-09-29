using Microsoft.AspNetCore.Mvc;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.ComponentModel.DataAnnotations;
using ProductsMicroservice.Core.Domain.Exceptions;
using System.Diagnostics;

namespace ProductsMicroService.API.Controllers
{
    /// <summary>
    /// Products Controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private const int UuidVersion4 = 4;

        private readonly IProductsGetterService _productsGetterService;
        private readonly IProductsAdderService _productsAdderService;
        private readonly IProductsDeleterService _productsDeleterService;
        private readonly IProductsUpdaterService _productsUpdaterService;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="productsUpdaterService"></param>
        /// <param name="productsGetterService"></param>
        /// <param name="productsAdderService"></param>
        /// <param name="productsDeleterService"></param>
        public ProductsController(IProductsUpdaterService productsUpdaterService, 
            IProductsGetterService productsGetterService, 
            IProductsAdderService productsAdderService, 
            IProductsDeleterService productsDeleterService)
        {
            _productsUpdaterService = productsUpdaterService;
            _productsGetterService = productsGetterService;
            _productsAdderService = productsAdderService;
            _productsDeleterService = productsDeleterService;
        }

        //GET /api/products
        /// <summary>
        /// get all products
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IEnumerable<ProductResponse?>> GetAllProductsAsync()
        {
            var products = await _productsGetterService.GetProductsAsync();
            return products;
        }

        //GET /api/products/search/product-id/xxxxxxxxxxxxxxxxxxx
        /// <summary>
        /// get products by productId
        /// </summary>
        /// <returns></returns>
        [HttpGet("search/product-id/{productId:guid}")]
        public async Task<ActionResult<ProductResponse>> GetProductByProductIdAsync([Required] Guid? productId)
        {
            ProductResponse? product = await _productsGetterService.GetProductByProductIdAsync(productId!.Value);

            if (product == null)
                return NotFound();

            return product;
        }

        //POST /api/products
        /// <summary>
        /// add a new product
        /// </summary>
        /// <param name="productAddRequest"></param>
        /// <param name="idempotencyKey">A canonical UUID v4 supplied by the client.</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddNewProductAsync(
            ProductAddRequest? productAddRequest,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            if (productAddRequest == null)
            {
                return BadRequest("The request body cannot be empty and must be a valid JSON.");
            }

            if (!TryParseCanonicalUuidV4(idempotencyKey, out Guid parsedIdempotencyKey))
            {
                throw new IdempotencyKeyInvalidException();
            }

            ProductAddResult result = await _productsAdderService.AddProductAsync(
                productAddRequest, parsedIdempotencyKey);

            Response.Headers["Idempotency-Outcome"] = result.IsReplay ? "replayed" : "created";
            Response.Headers["Idempotency-Replayed"] = result.IsReplay ? "true" : "false";

            //add location header in response like below
            //api/products/search/product-id/xxxxxxxxxxxxxxxxxxx
            return CreatedAtAction(nameof(GetProductByProductIdAsync),
                new{ productId = result.Product.ProductId}, result.Product);
        }

        private static bool TryParseCanonicalUuidV4(
            string? value,
            out Guid parsedKey)
        {
            return Guid.TryParseExact(value, "D", out parsedKey) &&
                   parsedKey.Version == UuidVersion4;
        }

        //PUT /api/products
        /// <summary>
        /// update a product
        /// </summary>
        /// <param name="productUpdateRequest"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> UpdateProductAsync(ProductUpdateRequest? productUpdateRequest)
        {
            if (productUpdateRequest == null)
            {
                return BadRequest("The request body cannot be empty and must be a valid JSON.");
            }

            var updatedProductResponse = await _productsUpdaterService.UpdateProductAsync(productUpdateRequest);
            return Ok(updatedProductResponse);
        }


        //DELETE /api/products/xxxxxxxxxxxxxxxxxxx
        /// <summary>
        /// delete a product by productId
        /// </summary>
        /// <param name="productId"></param>
        /// <param name="request">Contains the version originally read by the client.</param>
        /// <returns></returns>
        [HttpDelete("{productId:guid}")]
        public async Task<IActionResult> DeleteProductAsync(
            [Required] Guid? productId,
            [FromBody] ProductDeleteRequest? request)
        {
            if (request is null)
            {
                return BadRequest("The request body cannot be empty and must be a valid JSON.");
            }

            await _productsDeleterService.DeleteProductAsync(productId!.Value, request.Version);
            return Ok(true);
        }
    }
}
