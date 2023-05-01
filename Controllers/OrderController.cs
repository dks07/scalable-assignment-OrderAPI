using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderAPI.Models;
using OrderAPI.Services;
using System.Security.Claims;

namespace OrderAPI.Controllers
{
  [Authorize]
  [Route("api/[controller]")]
  [ApiController]
  public class OrderController : ControllerBase
  {
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
      _orderService = orderService;
    }


    [HttpGet]
    public async Task<IActionResult> GetOrders()
    {
      string userId = User.FindFirstValue(ClaimTypes.Name);
      var orders = await _orderService.GetOrdersAsync(userId);
      return Ok(orders);
    }

    [HttpGet("{id}", Name = "GetOrder")]
    [ActionName("GetOrder")]
    public async Task<IActionResult> GetOrder(string id)
    {
      string userId = User.FindFirstValue(ClaimTypes.Name);
      var order = await _orderService.GetOrderAsync(id, userId);

      if (order == null)
      {
        return NotFound();
      }

      return Ok(order);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderModel>> PlaceOrderAsync(OrderModel orderModel)
    {

      // Check if there are any validation errors
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }
      string userId = User.FindFirstValue(ClaimTypes.Name);
      // Save the order
      var order = await _orderService.CreateOrderAsync(orderModel, userId);
      return CreatedAtAction("GetOrder", new { id = order.Id }, order);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrder(string id)
    {
      string userId = User.FindFirstValue(ClaimTypes.Name);

      var result = await _orderService.DeleteOrderAsync(id, userId);
      if (!result)
      {
        return NotFound();
      }

      return Ok();
    }
  }
}
