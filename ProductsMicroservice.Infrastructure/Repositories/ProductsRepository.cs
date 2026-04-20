using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Diagnostics;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.Repositories
{
    public class ProductsRepository : IProductsRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ProductsRepository> _logger;

        public ProductsRepository(ApplicationDbContext dbContext, ILogger<ProductsRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetProductsAsync()
        {
            var activity = Activity.Current;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Fetching all products from database");

                var products = await _dbContext.Products.ToListAsync();

                stopwatch.Stop();

                int count = products.Count;

                // Metrics
                DiagnosticsConfig.GetProductsHistogram.Record(stopwatch.Elapsed.TotalSeconds);

                // Tracing
                activity?.SetTag("db.entity", "Product");
                activity?.SetTag("db.rows", count);

                // Logging
                _logger.LogInformation(
                    "Fetched {ProductCount} products from database in {ElapsedSeconds}s",
                    count,
                    stopwatch.Elapsed.TotalSeconds);

                return products;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "Error occurred while fetching products from database");

                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                throw;
            }
        }

        public async Task<Product?> GetProductByProductIdAsync(Guid productId)
        {
            var activity = Activity.Current;
            var stopwatch = Stopwatch.StartNew();

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
            {
                try
                {
                    _logger.LogInformation("Fetching product from database");

                    var product = await _dbContext.Products
                        .FirstOrDefaultAsync(p => p.ProductId == productId);

                    stopwatch.Stop();

                    // Metrics
                    DiagnosticsConfig.GetProductByProductIdHistogram
                        .Record(stopwatch.Elapsed.TotalSeconds);

                    // Tracing
                    activity?.SetTag("db.found", product != null);
                    activity?.SetTag("db.entity", "Product");
                    activity?.SetTag("product.id", productId);

                    if (product == null)
                    {
                        _logger.LogWarning(
                            "Product not found in database. Elapsed {ElapsedMs} ms",
                            stopwatch.Elapsed.TotalMilliseconds);

                        return null;
                    }

                    _logger.LogInformation(
                        "Product retrieved from database in {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);

                    return product;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _logger.LogError(ex,
                        "Error occurred while fetching product from database");

                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    throw;
                }
            }
        }

        public async Task<Product?> AddProductAsync(Product product)
        {
            var stopwatch = Stopwatch.StartNew();
            var activity = Activity.Current;

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = product.ProductId }))
            {
                try
                {
                    _logger.LogInformation("Attempting to insert product: {ProductName}", product.ProductName);

                    _dbContext.Products.Add(product);
                    await _dbContext.SaveChangesAsync();
                    stopwatch.Stop();

                    // Metric:Only low-cardinality tags are used for Metrics to prevent memory issues. 
                    DiagnosticsConfig.AddProductHistogram.Record(stopwatch.Elapsed.TotalSeconds);

                    DiagnosticsConfig.ProductsCounter.Add(1,
                        new KeyValuePair<string, object?>("product.id", product.ProductId),
                        new("status", "success")
                    );

                    // Tracing
                    activity?.SetTag("product.id", product.ProductId);
                    activity?.SetTag("db.entity", nameof(Product));
                    activity?.SetTag("product.name", product.ProductName);

                    // Logging
                    _logger.LogInformation("Product {ProductId} created successfully in {Elapsed}ms",
                        product.ProductId, stopwatch.Elapsed.TotalMilliseconds);

                    return product;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    //Metric
                    DiagnosticsConfig.ProductsCounter.Add(1,
                        new("product.id", product.ProductId),
                        new("status", "failed"),
                        new("error.type", ex.GetType().Name));

                    //Logging
                    _logger.LogError(ex, "Error occurred while inserting product into database");

                    //Trace 
                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    throw;
                }
            }
        }

        public async Task<Product?> UpdateProductAsync(Product product)
        {
            var activity = Activity.Current;
            var stopwatch = Stopwatch.StartNew();

            //Trace
            activity?.SetTag("db.entity", "Product");
            activity?.SetTag("product.id", product.ProductId);

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = product.ProductId }))
            {
                try
                {
                    _logger.LogInformation("Updating product in database");

                    Product? existingProduct = await _dbContext.Products
                        .FirstOrDefaultAsync(p => p.ProductId == product.ProductId);

                    if (existingProduct == null)
                    {
                        stopwatch.Stop();

                        activity?.SetTag("db.found", false);

                        _logger.LogWarning(
                            "Product not found for update. Elapsed {ElapsedMs} ms",
                            stopwatch.Elapsed.TotalMilliseconds);

                        return null;
                    }

                    // Apply updates
                    existingProduct.ProductName = product.ProductName;
                    existingProduct.UnitPrice = product.UnitPrice;
                    existingProduct.QuantityInStock = product.QuantityInStock;
                    existingProduct.Version++;

                    int affectedRowsCount = await _dbContext.SaveChangesAsync();

                    stopwatch.Stop();

                    //Metrics
                    DiagnosticsConfig.UpdateProductHistogram
                        .Record(stopwatch.Elapsed.TotalSeconds);

                    //Trace
                    activity?.SetTag("db.rows_affected", affectedRowsCount);
                    activity?.SetTag("db.found", true);


                    if (affectedRowsCount == 0)
                    {
                        _logger.LogWarning(
                            "Update operation completed but no rows affected. Elapsed {ElapsedMs} ms",
                            stopwatch.Elapsed.TotalMilliseconds);

                        return null;
                    }

                    _logger.LogInformation(
                        "Product updated in database in {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);

                    return existingProduct;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    stopwatch.Stop();

                    _logger.LogWarning(ex,
                        "Concurrency conflict occurred while updating product");

                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, "Concurrency conflict");

                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _logger.LogError(ex,
                        "Error occurred while updating product in database");

                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    throw;
                }
            }
        }

        public async Task<bool> DeleteProductAsync(Guid productId)
        {
            var activity = Activity.Current;
            var stopwatch = Stopwatch.StartNew();

            //Trace
            activity?.SetTag("db.entity", "Product");
            activity?.SetTag("product.id", productId);

            using (_logger.BeginScope(new Dictionary<string, object> { ["ProductId"] = productId }))
            {
                try
                {
                    _logger.LogInformation("Deleting product from database");

                    var existingProduct = await _dbContext.Products
                        .FirstOrDefaultAsync(p => p.ProductId == productId);

                    if (existingProduct == null)
                    {
                        stopwatch.Stop();

                        activity?.SetTag("db.found", false);

                        _logger.LogWarning(
                            "Product not found for deletion. Elapsed {ElapsedMs} ms",
                            stopwatch.Elapsed.TotalMilliseconds);

                        return false;
                    }

                    _dbContext.Products.Remove(existingProduct);

                    int affectedRowsCount = await _dbContext.SaveChangesAsync();

                    stopwatch.Stop();

                    //Metrics
                    DiagnosticsConfig.DeleteProductHistogram
                        .Record(stopwatch.Elapsed.TotalSeconds);

                    //Trace
                    activity?.SetTag("db.found", true);
                    activity?.SetTag("db.rows_affected", affectedRowsCount);

                    if (affectedRowsCount > 0)
                    {
                        DiagnosticsConfig.ProductsCounter.Add(-1,
                            new KeyValuePair<string, object?>("product.id", productId));

                        _logger.LogInformation(
                            "Product deleted from database in {ElapsedMs} ms",
                            stopwatch.Elapsed.TotalMilliseconds);

                        return true;
                    }

                    _logger.LogWarning(
                        "Delete operation completed but no rows affected. Elapsed {ElapsedMs} ms",
                        stopwatch.Elapsed.TotalMilliseconds);

                    return false;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();

                    _logger.LogError(ex,
                        "Error occurred while deleting product from database");

                    activity?.AddException(ex);
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                    throw;
                }
            }
        }
    }
}
