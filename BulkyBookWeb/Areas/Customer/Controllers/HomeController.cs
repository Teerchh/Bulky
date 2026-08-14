using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers;

[Area("Customer")]
public class HomeController(ILogger<HomeController> logger, IUnitOfWork unit) : Controller
{
    public IActionResult Index()
    {
        var products = unit.Product.GetAll(includeProperties: "Category,ProductImages");
        return View(products);
    }

    public IActionResult Details(int productId)
    {
        var cart = new ShoppingCart()
        {
            Product = unit.Product.Get(u => u.Id == productId, includeProperties: "Category,ProductImages"),
            Count = 1,
            ProductId = productId
        };
        return View(cart);
    }

    [HttpPost]
    [Authorize]
    public IActionResult Details(ShoppingCart shoppingCart)
    {
        var claimsIdentity = (ClaimsIdentity)User.Identity;
        var userid = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
        shoppingCart.ApplicationUserId = userid;

        var cartExist = unit.ShoppingCart.Get(u => u.ApplicationUserId == shoppingCart.ApplicationUserId && u.ProductId == shoppingCart.ProductId);

        if (cartExist != null)
        {
            cartExist.Count += shoppingCart.Count;
            unit.ShoppingCart.Update(cartExist);
        }
        else
        {
            unit.ShoppingCart.Add(shoppingCart);
        }
        TempData["success"] = "Cart updated successfully";

        unit.Save();
        HttpContext.Session.SetInt32(SD.SessionCart,
            unit.ShoppingCart.GetAll().Where(u => u.ApplicationUserId == userid).Count());


        return RedirectToAction(nameof(Index));

    }
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
