using System.ComponentModel.DataAnnotations;

namespace ProductsMicroservice.Core.DTO;

public class ProductUpdateRequest
{
    [Required(ErrorMessage = "{0} can't be blank")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "{0} can't be blank")]
    public string? ProductName { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "{0} should between ${1} and ${2}")]
    public double? UnitPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "{0} should between {1} and {2}")]
    public int? QuantityInStock { get; set; }
}