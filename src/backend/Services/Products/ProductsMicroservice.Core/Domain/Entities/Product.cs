using System.ComponentModel.DataAnnotations;

namespace ProductsMicroservice.Core.Domain.Entities
{
    public class Product
    {
        //ef core will generate ProductId automatically,because ProductId is primary key
        [Key]
        public Guid ProductId { get; set; }

        // Canonical, internal-only key used for uniqueness checks.
        public string ProductName { get; set; } = string.Empty;

        // The user-entered name shown by APIs, logs, and notifications.
        public string DisplayName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int QuantityInStock { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }  // Concurrency Token
    }
}
