using Microsoft.AspNetCore.Mvc;
using Tutorial9.Models;

namespace Tutorial9.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpPost]
    public async Task<IActionResult> AddProduct([FromBody] ProductRequest request)
    {
        try
        {
            int insertedId = await _warehouseService.AddProductToWarehouseAsync(request);
            return Ok(new { IdProductWarehouse = insertedId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
    
    [HttpPost("procedure")]
    public async Task<IActionResult> AddProductViaProcedure([FromBody] ProductRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest("Amount must be greater than 0.");

        try
        {
            int insertedId = await _warehouseService.AddProductViaProcedureAsync(request);
            return Ok(new { IdProductWarehouse = insertedId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

}

