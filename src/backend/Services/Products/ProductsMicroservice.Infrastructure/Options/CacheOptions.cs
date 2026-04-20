namespace ProductsMicroservice.Infrastructure.Options
{
    public class CacheOptions
    {
        public int DefaultExpirationMinutes { get; set; } = 60;
        public int NegativeCacheExpirationMinutes { get; set; } = 5;
        public string NullValuePlaceholder { get; set; } = "null_value";
    }
}
