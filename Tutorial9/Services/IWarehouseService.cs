using Tutorial9.Models;

public interface IWarehouseService
{
    Task<int> AddProductToWarehouseAsync(ProductRequest request);
    Task<int> AddProductViaProcedureAsync(ProductRequest request);
}