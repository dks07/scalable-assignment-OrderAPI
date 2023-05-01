using System.ComponentModel.DataAnnotations;

namespace OrderAPI.Models;

public class OrderItemModel
{
  [Required]
  public string ProductId { get; set; }

  [Required]
  [Range(1, int.MaxValue, ErrorMessage = "The Quantity field must be greater than or equal to 0.")]
  public int Quantity { get; set; }
}