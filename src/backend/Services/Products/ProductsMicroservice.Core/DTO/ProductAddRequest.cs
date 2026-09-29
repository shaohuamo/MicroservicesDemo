using System.ComponentModel.DataAnnotations;

namespace ProductsMicroservice.Core.DTO;

public class ProductAddRequest
{
    [Required(ErrorMessage = "{0} can't be blank")]
    [StringLength(50, ErrorMessage = "{0} cannot exceed {1} characters")]
    [RegularExpression(@"^(?!.* {2})[\p{L}\p{N}](?:[\p{L}\p{N} .'’&()+/_-]*[\p{L}\p{N}])?$", ErrorMessage = "{0} contains unsupported characters")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = "{0} can't be blank")]
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "{0} should be between ${1} and ${2}")]
    public decimal? UnitPrice { get; set; }

    [Required(ErrorMessage = "{0} can't be blank")]
    [Range(0, 1000000, ErrorMessage = "{0} should be between {1} and {2}")]
    public int? QuantityInStock { get; set; }
}
