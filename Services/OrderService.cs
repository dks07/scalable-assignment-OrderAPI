using Microsoft.AspNetCore.Authentication;
using MongoDB.Bson;
using MongoDB.Driver;
using OrderAPI.Models;
using OrderAPI.Settings;
using RabbitMQ.Client;
using System.Net.Http.Headers;
using System.Text;

namespace OrderAPI.Services
{
  public class OrderService : IOrderService
  {
    private readonly IMongoCollection<Order> _orders;
    private readonly HttpClient _httpClient;
    private readonly string _inventoryServiceBaseUrl;
    private readonly IModel _rabbitMQChannel;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderService(IMongoClient client, IOrderDatabaseSettings settings, IHttpClientFactory httpClientFactory, IAppSettings appSettings, IModel rabbitMQChannel, IHttpContextAccessor httpContextAccessor)
    {
      var database = client.GetDatabase(settings.DatabaseName);

      _orders = database.GetCollection<Order>(settings.OrderCollectionName);
      _httpContextAccessor = httpContextAccessor;
      _httpClient = httpClientFactory.CreateClient();
      _inventoryServiceBaseUrl = appSettings.InventoryServiceBaseUrl;
      _rabbitMQChannel = rabbitMQChannel;
      _rabbitMQChannel.ExchangeDeclare("shipping", ExchangeType.Fanout);
      _rabbitMQChannel.QueueDeclare("shipping", true, false);
      _rabbitMQChannel.QueueBind("shipping", "shipping", "");
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
      if (string.IsNullOrEmpty(order.Id))
      {
        order.Id = ObjectId.GenerateNewId().ToString();
      }
      await _orders.InsertOneAsync(order);
      return order;
    }

    public async Task<bool> DeleteOrderAsync(string id, string userId)
    {
      var orderModel = await GetOrderAsync(id, userId);
      if (orderModel == null) return false;
      var productIds = orderModel.Products.Select(o => o.ProductId).ToList();

      // get the current token from the http context
      string accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
      List<ProductModel>? products = await FetchProducts(productIds);
      if (products == null)
      {
        throw new ArgumentException("No Product found.");
      }

      var deleteResult = await _orders.DeleteOneAsync(o => o.Id == id && o.UserId == userId);


      foreach (var orderItem in orderModel.Products)
      {
        var product = products.FirstOrDefault(p => p.Id == orderItem.ProductId);
        product!.Quantity += orderItem.Quantity;
        await UpdateProductAsync(product.Id, product);
      }


      // Publish message to delete a shipping
      var shippingMessage = new
      {
        OrderId = id,
        Operation = "Delete"
      };
      var shippingMessageBody = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(shippingMessage));
      _rabbitMQChannel.BasicPublish("shipping", "", null, shippingMessageBody);
      return deleteResult.DeletedCount == 1;
    }
    public async Task<List<OrderModel>> GetOrdersAsync(string userId)
    {
      var cursor = await _orders.FindAsync(order => order.UserId == userId);
      var orders = await cursor.ToListAsync();
      return orders.Select(order =>
        {
          var orderModel = new OrderModel
          {
            Id = order.Id,
            Products = new List<OrderItemModel>(),
            ShippingAddress = order.ShippingAddress,
          };
          if (order.OrderItems != null)
            foreach (var orderProductModel in order.OrderItems.Select(item => new OrderItemModel
            {
              ProductId = item.ProductId,
              Quantity = item.Quantity
            }))
            {
              orderModel.Products.Add(orderProductModel);
            }

          return orderModel;
        })
        .ToList();
    }

    public async Task<OrderModel?> GetOrderAsync(string id, string userId)
    {
      var cursor = await _orders.FindAsync(o => o.Id == id && o.UserId == userId);
      var order = await cursor.FirstOrDefaultAsync();
      if (order == null)
      {
        return null;
      }
      else
      {
        var orderModel = new OrderModel
        {
          Id = order.Id,
          Products = new List<OrderItemModel>(),
          ShippingAddress = order.ShippingAddress
        };
        if (order.OrderItems != null)
          foreach (var orderProductModel in order.OrderItems.Select(item => new OrderItemModel
                   {
                     ProductId = item.ProductId,
                     Quantity = item.Quantity
                   }))
          {
            orderModel.Products.Add(orderProductModel);
          }

        return orderModel;
      }
    }

    public async Task<OrderModel?> CreateOrderAsync(OrderModel orderModel, string userId)
    {

      // Check if all products exist and have enough quantity
      var productIds = orderModel.Products.Select(o => o.ProductId).ToList();

      // get the current token from the http context
      string accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
      _httpClient.DefaultRequestHeaders
        .Accept
        .Add(new MediaTypeWithQualityHeaderValue("application/json"));
      List<ProductModel>? products = await FetchProducts(productIds);
      if (products == null)
      {
        throw new ArgumentException("No Product found.");
      }
      foreach (var orderItem in orderModel.Products)
      {
        var product = products.FirstOrDefault(p => p.Id == orderItem.ProductId);
        if (product == null)
        {
          throw new ArgumentException($"Product with id {orderItem.ProductId} not found.");
        }

        if (product.Quantity < orderItem.Quantity)
        {
          throw new ArgumentException($"Product with id {orderItem.ProductId} does not have enough quantity.");
        }
      }

      List<OrderItem> orderItems = new List<OrderItem>();
      foreach (var orderItem in orderModel.Products)
      {

        var product = products.FirstOrDefault(p => p.Id == orderItem.ProductId);
        orderItems.Add(new OrderItem
        {
          Quantity = orderItem.Quantity,
          ProductId = orderItem.ProductId,
          TotalPrice = orderItem.Quantity * product!.Price,
          UnitPrice = product.Price
        });
      }
      // Deduct product quantities and create order
      var newOrder = new Order
      {
        UserId = userId,
        OrderItems = orderItems,
        TotalAmount = orderItems.Sum(o => o.TotalPrice),
        OrderDate = DateTime.Now,
        ShippingAddress = orderModel.ShippingAddress,
      };

      foreach (var orderItem in orderModel.Products)
      {
        var product = products.FirstOrDefault(p => p.Id == orderItem.ProductId);
        product!.Quantity -= orderItem.Quantity;
        await UpdateProductAsync(product.Id, product);
      }

      await CreateOrderAsync(newOrder);
      orderModel.Id = newOrder.Id;

      // Publish message to create a shipping
      var shippingMessage = new
      {
        OrderId = newOrder.Id,
        newOrder.UserId,
        newOrder.ShippingAddress,
        Operation = "Create"
      };
      var shippingMessageBody = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(shippingMessage));
      _rabbitMQChannel.BasicPublish("shipping", "", null, shippingMessageBody);
      return orderModel;
    }

    private async Task UpdateProductAsync(string productId, ProductModel product)
    {
      var requestUrl = $"{_inventoryServiceBaseUrl}/{productId}";
      var response = await _httpClient.PutAsync(requestUrl, new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(product), Encoding.UTF8,
        "application/json"));
      if (!response.IsSuccessStatusCode)
      {
        throw new Exception("Failed to update products in Inventory.");
      }
    }

    private async Task<List<ProductModel>?> FetchProducts(List<string> productIds)
    {

      List<ProductModel> productModels = new List<ProductModel>();
      foreach (var productId in productIds)
      {
        var requestUrl = $"{_inventoryServiceBaseUrl}/{productId}";
        var response = await _httpClient.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
          throw new Exception($"Failed to fetch product with id '{productId}' from Inventory.");
        }

        var productsJson = await response.Content.ReadAsStringAsync();
        var product = Newtonsoft.Json.JsonConvert.DeserializeObject<ProductModel>(productsJson);
        if (product != null) productModels.Add(product);
      }

      return productModels;
    }
  }
}
