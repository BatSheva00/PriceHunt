using Microsoft.AspNetCore.Mvc;
using PriceHunt.Api.Suppliers;

namespace PriceHunt.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly SupplierCatalog _catalog;

    public SuppliersController(SupplierCatalog catalog) => _catalog = catalog;

    [HttpGet]
    public ActionResult<IReadOnlyList<string>> Get() => Ok(_catalog.Names);
}
