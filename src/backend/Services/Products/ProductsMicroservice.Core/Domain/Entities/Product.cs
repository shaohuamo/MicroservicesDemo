using System.ComponentModel.DataAnnotations;

namespace ProductsMicroservice.Core.Domain.Entities
{
    public class Product
    {
        //ef core will generate ProductId automatically,because ProductId is primary key
        [Key]
        public Guid ProductId { get; set; }
        public string? ProductName { get; set; }
        public double? UnitPrice { get; set; }
        public int? QuantityInStock { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }  // Concurrency Token
    }
}
