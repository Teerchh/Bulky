using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class CompanyController(IUnitOfWork unit) : Controller
{
    public IActionResult Index()
    {
        var Companys = unit.Company.GetAll();

        return View(Companys);
    }

    public IActionResult Upsert(int? id)
    {
        if (id == null || id == 0)
        {
            return View(new Company());
        }
        else
        {
            Company company = unit.Company.Get(u => u.Id == id);
            return View(company);
        }
    }

    [HttpPost]
    public IActionResult Upsert(Company Company)
    {
        if (ModelState.IsValid)
        {
            if (Company.Id == 0)
            {
                unit.Company.Add(Company);
                TempData["success"] = "Company created Successfully";
            }

            else
            {
                unit.Company.Update(Company);
                TempData["success"] = "Company updated Successfully";

            }

            unit.Save();
            return RedirectToAction("Index");
        }
        else
        {
            if (Company.Id == 0)
            {
                TempData["error"] = "Error creating Company";
            }
            else
            {
                TempData["error"] = "Error updating Company";
            }

            IEnumerable<SelectListItem> CategoryList = unit.Category.GetAll().Select(u => new SelectListItem
            {
                Text = u.Name,
                Value = u.Id.ToString()
            });

            return View(Company);

        }
    }

    #region APICALLS
    [HttpGet]
    public IActionResult GetAll()
    {
        var Companys = unit.Company.GetAll();
        return Json(new { data = Companys });
    }


    [HttpDelete]
    public IActionResult Delete(int? id)
    {
        var companyToDelete = unit.Company.Get(u => u.Id == id);
        if (companyToDelete == null)
        {
            return Json(new { success = false, message = "Error while deleting" });
        }

        unit.Company.Remove(companyToDelete);
        unit.Save();

        return Json(new { success = true, message = "Deleted Successfully" });

    }
    #endregion
}
