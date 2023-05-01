using OrderAPI.Models;

namespace OrderAPI.Services
{
  public interface IOrderService
  {
    Task<List<OrderModel>> GetOrdersAsync(string userId);
    Task<OrderModel?> GetOrderAsync(string id, string userId);
    Task<OrderModel?> CreateOrderAsync(OrderModel orderModel, string userId);
    Task<bool> DeleteOrderAsync(string id, string userId);
  }
}