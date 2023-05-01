using System.ComponentModel.DataAnnotations;

namespace OrderAPI.Models;

public class OrderModel
{
  public string Id { get; set; }

  [Required]
  [MinLength(1)]
  public List<OrderItemModel> Products { get; set; }

  [Required]
  public string ShippingAddress { get; set; }
}