using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetFlow.Application.Services;
using VetFlow.Domain.Enums;
using VetFlow.Web.Helpers;
using VetFlow.Web.Models;

namespace VetFlow.Web.Controllers;

public class InventoryController(IInventoryService inventory) : Controller
{
    public async Task<IActionResult> Index(string? q, ProductCategory? category, bool low = false, bool inactive = false, CancellationToken ct = default)
    {
        ViewBag.Query = q;
        ViewBag.Category = category;
        ViewBag.Low = low;
        ViewBag.Inactive = inactive;
        return View(await inventory.SearchAsync(q, category, low, inactive, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await inventory.GetAsync(id, ct));

    [Authorize(Policy = Policies.ManageInventory)]
    public IActionResult Create() => View("Form", new ProductFormModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Create(ProductFormModel model, CancellationToken ct)
    {
        if (ModelState.IsValid && await inventory.SkuExistsAsync(model.Sku, null, ct))
        {
            ModelState.AddModelError(nameof(model.Sku), "This SKU is already used.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        var product = await inventory.CreateAsync(model.ToInput(), User.UserName(), ct);
        TempData["Success"] = $"'{product.Name}' was added to the catalogue.";
        return RedirectToAction(nameof(Details), new { id = product.Id });
    }

    [Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) => View("Form", ProductFormModel.From((await inventory.GetAsync(id, ct)).Product));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Edit(int id, ProductFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid && await inventory.SkuExistsAsync(model.Sku, id, ct))
        {
            ModelState.AddModelError(nameof(model.Sku), "This SKU is already used.");
        }

        if (!ModelState.IsValid) return View("Form", model);

        await inventory.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Product saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Restock(int id, decimal quantity, string? reference, CancellationToken ct)
    {
        await inventory.RestockAsync(id, quantity, reference, User.UserName(), ct);
        TempData["Success"] = $"Stock increased by {Ui.Quantity(quantity)}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.ManageInventory)]
    public async Task<IActionResult> Adjust(int id, decimal counted, string? reason, CancellationToken ct)
    {
        await inventory.AdjustAsync(id, counted, reason, User.UserName(), ct);
        TempData["Success"] = "Stock corrected to the counted quantity.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
