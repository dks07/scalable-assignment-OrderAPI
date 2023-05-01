using System.ComponentModel.DataAnnotations;

namespace OrderAPI.Models;

public class ProductModel
{
  public string Id { get; set; }

  [Required(ErrorMessage = "Product name is required.")]
  public string Name { get; set; }

  [Required(ErrorMessage = "Product description is required.")]
  public string Description { get; set; }

  [Required(ErrorMessage = "Product price is required.")]
  [Range(0.01, double.MaxValue, ErrorMessage = "Product price must be greater than 0.")]
  public decimal Price { get; set; }

  [Required]
  [Range(0, int.MaxValue, ErrorMessage = "The Quantity field must be greater than or equal to 0.")]
  public int Quantity { get; set; }
}