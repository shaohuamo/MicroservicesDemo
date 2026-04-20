using Microsoft.AspNetCore.Mvc;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using System.ComponentModel.DataAnnotations;

namespace ProductsMicroService.API.Controllers
{
    /// <summary>
    /// Products Controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
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
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddNewProductAsync(ProductAddRequest? productAddRequest)
        {
            if (productAddRequest == null)
            {
                return BadRequest("The request body cannot be empty and must be a valid JSON.");
            }

            var addedProductResponse = await _productsAdderService.AddProductAsync(productAddRequest);

            if (addedProductResponse == null)
            {
                return Problem("Error in adding product");
            }

            //add location header in response like below
            //api/products/search/product-id/xxxxxxxxxxxxxxxxxxx
            return CreatedAtAction(nameof(GetProductByProductIdAsync),
                new{ productId = addedProductResponse.ProductId}, addedProductResponse);
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

            if (updatedProductResponse != null)
                return Ok(updatedProductResponse);
            else
                return Problem("Invalid ProductId");
        }


        //DELETE /api/products/xxxxxxxxxxxxxxxxxxx
        /// <summary>
        /// delete a product by productId
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpDelete("{ProductId:guid}")]
        public async Task<IActionResult> DeleteProductAsync([Required] Guid? productId)
        {
            bool isDeleted = await _productsDeleterService.DeleteProductAsync(productId!.Value);
            if (isDeleted)
                return Ok(true);
            else
                return Problem("Invalid ProductId");
        }
    }
}
