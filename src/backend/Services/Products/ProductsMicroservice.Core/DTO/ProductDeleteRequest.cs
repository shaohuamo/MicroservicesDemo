using System.ComponentModel.DataAnnotations;

namespace ProductsMicroservice.Core.DTO;

public sealed class ProductDeleteRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "{0} should be greater than zero")]
    public int Version { get; set; }
}
