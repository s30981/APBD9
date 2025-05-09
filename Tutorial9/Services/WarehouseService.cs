using System.Data;
using Microsoft.Data.SqlClient;
using Tutorial9.Models;

public class WarehouseService : IWarehouseService
{
    private readonly string _connectionString;

    public WarehouseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<int> AddProductToWarehouseAsync(ProductRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var checkProductCmd = new SqlCommand("SELECT 1 FROM Product WHERE IdProduct = @IdProduct", connection, transaction);
            checkProductCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            if (await checkProductCmd.ExecuteScalarAsync() == null)
                throw new Exception("Product not found.");

            var checkWarehouseCmd = new SqlCommand("SELECT 1 FROM Warehouse WHERE IdWarehouse = @IdWarehouse", connection, transaction);
            checkWarehouseCmd.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            if (await checkWarehouseCmd.ExecuteScalarAsync() == null)
                throw new Exception("Warehouse not found.");

            if (request.Amount <= 0)
                throw new Exception("Amount must be greater than 0.");

            var orderCmd = new SqlCommand(@"
                SELECT TOP 1 * FROM [Order]
                WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @RequestCreatedAt
                ORDER BY CreatedAt", connection, transaction);
            orderCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            orderCmd.Parameters.AddWithValue("@Amount", request.Amount);
            orderCmd.Parameters.AddWithValue("@RequestCreatedAt", request.CreatedAt);
            var reader = await orderCmd.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                await reader.DisposeAsync();
                throw new Exception("No matching order found.");
            }

            await reader.ReadAsync();
            int idOrder = reader.GetInt32(reader.GetOrdinal("IdOrder"));
            reader.Dispose();

            var checkFulfilledCmd = new SqlCommand("SELECT 1 FROM Product_Warehouse WHERE IdOrder = @IdOrder", connection, transaction);
            checkFulfilledCmd.Parameters.AddWithValue("@IdOrder", idOrder);
            if (await checkFulfilledCmd.ExecuteScalarAsync() != null)
                throw new Exception("Order already fulfilled.");

            var now = DateTime.Now;
            var updateOrderCmd = new SqlCommand("UPDATE [Order] SET FulfilledAt = @FulfilledAt WHERE IdOrder = @IdOrder", connection, transaction);
            updateOrderCmd.Parameters.AddWithValue("@FulfilledAt", now);
            updateOrderCmd.Parameters.AddWithValue("@IdOrder", idOrder);
            await updateOrderCmd.ExecuteNonQueryAsync();

            var priceCmd = new SqlCommand("SELECT Price FROM Product WHERE IdProduct = @IdProduct", connection, transaction);
            priceCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            decimal price = (decimal)(await priceCmd.ExecuteScalarAsync() ?? throw new Exception("Product price not found."));

            decimal totalPrice = price * request.Amount;

            var insertCmd = new SqlCommand(@"
                INSERT INTO Product_Warehouse
                (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                OUTPUT INSERTED.IdProductWarehouse
                VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)", connection, transaction);
            insertCmd.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            insertCmd.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            insertCmd.Parameters.AddWithValue("@IdOrder", idOrder);
            insertCmd.Parameters.AddWithValue("@Amount", request.Amount);
            insertCmd.Parameters.AddWithValue("@Price", totalPrice);
            insertCmd.Parameters.AddWithValue("@CreatedAt", now);

            int idProductWarehouse = (int)await insertCmd.ExecuteScalarAsync();

            await transaction.CommitAsync();

            return idProductWarehouse;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    public async Task<int> AddProductViaProcedureAsync(ProductRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand("AddProductToWarehouse", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
        command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

        try
        {
            var result = await command.ExecuteScalarAsync();

            if (result == null || !int.TryParse(result.ToString(), out int newId))
                throw new Exception("Procedure did not return a valid ID.");

            return newId;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException($"SQL Error: {ex.Message}", ex);
        }
    }
}
