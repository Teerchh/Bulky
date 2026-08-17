using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bulky.Models;

public class ProductImage
{
    public int Id { get; set; }
    [Required]
    public string ImageUrl { get; set; }
    public int ProductId { get; set; }
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; }

    /// <summary>True when this image is the product's front cover (shown on cards/listings).</summary>
    public bool IsFrontCover { get; set; }
}
