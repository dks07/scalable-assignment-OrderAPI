namespace OrderAPI.Settings;

public interface IOrderDatabaseSettings
{
  string OrderCollectionName { get; set; }
  string ConnectionString { get; set; }
  string DatabaseName { get; set; }
}