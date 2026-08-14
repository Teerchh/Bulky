using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BulkyBookWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class CategoryController(IUnitOfWork unit) : Controller
{
    public IActionResult Index()
    {
        var objCategoryList = unit.Category.GetAll();
        return View(objCategoryList);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Create(Category category)
    {
        if (category.Name == category.DisplayOrder.ToString())
        {
            ModelState.AddModelError("name", "DIsplayOrder != Name");
        }
        if (ModelState.IsValid)
        {
            unit.Category.Add(category);
            unit.Save();
            TempData["success"] = "Category created Successfully";
            return RedirectToAction("Index");
        }
        TempData["error"] = "Error creating category";

        return View();
    }

    public IActionResult Edit(int? id)
    {
        if (id == null || id == 0)
        {
            return NotFound();
        }
        var category = unit.Category.Get(u => u.Id == id);
        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    [HttpPost]
    public IActionResult Edit(Category category)
    {
        if (ModelState.IsValid)
        {
            unit.Category.Update(category);
            unit.Save();
            TempData["success"] = "Category updated Successfully";
            return RedirectToAction("Index");
        }
        TempData["error"] = "Error updating category";
        return View();
    }

    public IActionResult Delete(int? id)
    {
        if (id == null || id == 0)
        {
            return NotFound();
        }
        var category = unit.Category.Get(u => u.Id == id);
        if (category == null)
        {
            return NotFound();
        }

        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    public IActionResult DeletePOST(int? id)
    {
        var category = unit.Category.Get(u => u.Id == id);
        if (category == null)
        {
            return NotFound();
        }
        unit.Category.Remove(category);
        unit.Save();
        TempData["success"] = "Category deleted Successfully";
        return RedirectToAction("Index");
    }
}
