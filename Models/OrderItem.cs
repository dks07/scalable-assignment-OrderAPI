using MongoDB.Bson.Serialization.Attributes;

namespace OrderAPI.Models;

public class OrderItem
{
  [BsonElement("ProductId")]
  public string ProductId { get; set; }

  [BsonElement("Quantity")]
  public int Quantity { get; set; }

  [BsonElement("UnitPrice")]
  public decimal UnitPrice { get; set; }

  [BsonElement("TotalPrice")]
  public decimal TotalPrice { get; set; }
}