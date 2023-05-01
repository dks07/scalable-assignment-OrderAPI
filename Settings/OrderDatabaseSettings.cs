namespace OrderAPI.Settings
{
  public class OrderDatabaseSettings : IOrderDatabaseSettings
  {
    public string OrderCollectionName { get; set; }
    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
  }
}
