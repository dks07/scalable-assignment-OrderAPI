using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderAPI.Models;

public class Order
{
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  public string Id { get; set; }

  [BsonElement("UserId")]
  public string UserId { get; set; }

  public List<OrderItem> OrderItems { get; set; }

  [BsonElement("TotalAmount")]
  public decimal TotalAmount { get; set; }

  [BsonElement("OrderDate")]
  public DateTime OrderDate { get; set; }

  [BsonElement("Address")]
  public string ShippingAddress { get; set; }
}