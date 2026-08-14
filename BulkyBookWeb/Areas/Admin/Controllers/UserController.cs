using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class UserController(IUnitOfWork unit, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult RoleManagement(string userId)
    {
        var RoleVM = new RoleManagementVM()
        {
            User = unit.ApplicationUser.Get(u => u.Id == userId, includeProperties: "Company"),
            RoleList = roleManager.Roles.Select(i => new SelectListItem
            {
                Text = i.Name,
                Value = i.Name
            }),
            CompanyList = unit.Company.GetAll().Select(i => new SelectListItem
            {
                Text = i.Name,
                Value = i.Id.ToString()
            })
        };
        RoleVM.User.Role = userManager.GetRolesAsync(unit.ApplicationUser.Get(u => u.Id == userId))
            .GetAwaiter().GetResult().FirstOrDefault();


        return View(RoleVM);
    }

    [HttpPost]
    public IActionResult RoleManagement([Bind(Prefix = "User")] ApplicationUser UpdatedUser)
    {
        var oldRole = userManager.GetRolesAsync(unit.ApplicationUser.Get(u => u.Id == UpdatedUser.Id))
             .GetAwaiter().GetResult().FirstOrDefault();

        var user = unit.ApplicationUser.Get(u => u.Id == UpdatedUser.Id);

        if (!(UpdatedUser.Role == oldRole))
        {
            // a role was updated
            if (UpdatedUser.Role == SD.Role_Company)
            {
                user.CompanyId = UpdatedUser.CompanyId;
            }
            if (oldRole == SD.Role_Company)
            {
                user.CompanyId = null;
            }

            unit.ApplicationUser.Update(user);
            unit.Save();

            userManager.RemoveFromRoleAsync(user, oldRole).GetAwaiter().GetResult();
            userManager.AddToRoleAsync(user, UpdatedUser.Role).GetAwaiter().GetResult();
        }
        else
        {
            if (oldRole == SD.Role_Company && user.CompanyId != UpdatedUser.CompanyId)
            {
                user.CompanyId = UpdatedUser.CompanyId;
                unit.ApplicationUser.Update(user);
                unit.Save();
            }
        }



        return RedirectToAction(nameof(Index));
    }

    #region APICALLS
    [HttpGet]
    public IActionResult GetAll()
    {
        var Users = unit.ApplicationUser.GetAll(includeProperties: "Company");

        foreach (var user in Users)
        {
            user.Role = userManager.GetRolesAsync(user).GetAwaiter().GetResult().FirstOrDefault();

            if (user.Company == null)
            {
                user.Company = new() { Name = "" };
            }
        }
        return Json(new { data = Users });
    }


    [HttpPost]
    public IActionResult LockUnlock([FromBody] string id)
    {
        var user = unit.ApplicationUser.Get(u => u.Id == id);
        if (user == null)
        {
            return Json(new { success = false, message = "Error while Locking/Unlocking" });
        }

        if (user.LockoutEnd != null && user.LockoutEnd > DateTime.Now)
        {
            //user is locked unlock user
            user.LockoutEnd = DateTime.Now;
        }
        else
        {
            user.LockoutEnd = DateTime.Now.AddYears(1000);
        }
        unit.ApplicationUser.Update(user);
        unit.Save();
        return Json(new { success = true, message = "Operation successful" });
    }
    #endregion
}
