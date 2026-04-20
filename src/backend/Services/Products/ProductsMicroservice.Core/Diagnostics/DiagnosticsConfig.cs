using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ProductsMicroservice.Core.Diagnostics
{
    public static class DiagnosticsConfig
    {
        public const string ServiceName = "Product.Api";

        public static readonly ActivitySource Source = new(ServiceName);

        public static Meter ProductMeter = new(ServiceName);

        public static readonly UpDownCounter<int> ProductsCounter = ProductMeter.CreateUpDownCounter<int>("current_products", "products", "Number of products");

        public static readonly Histogram<double> AddProductHistogram = ProductMeter.CreateHistogram(
            "product.add_product.latency",
            unit: "s",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });

        public static readonly Histogram<double> GetProductsHistogram = ProductMeter.CreateHistogram(
            "product.get_products.latency",
            unit: "s",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });

        public static readonly Histogram<double> GetProductByProductIdHistogram = ProductMeter.CreateHistogram(
            "product.get_product_by_productId.latency",
            unit: "s",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });

        public static readonly Histogram<double> UpdateProductHistogram = ProductMeter.CreateHistogram(
            "product.update_product.latency",
            unit: "s",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });

        public static readonly Histogram<double> DeleteProductHistogram = ProductMeter.CreateHistogram(
            "product.delete_product.latency",
            unit: "s",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10]
            });
    }
}
