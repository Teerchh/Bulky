using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public class ProductController(IUnitOfWork unit, IWebHostEnvironment webHost) : Controller
{
    public IActionResult Index()
    {
        var products = unit.Product.GetAll(includeProperties: "Category");

        return View(products);
    }

    public IActionResult Upsert(int? id)
    {
        IEnumerable<SelectListItem> CategoryList = unit.Category.GetAll().Select(u => new SelectListItem
        {
            Text = u.Name,
            Value = u.Id.ToString()
        });

        ProductVM productvm = new()
        {
            Product = new Product(),
            CategoryList = CategoryList,
        };

        if (id == null || id == 0)
        {
            return View(productvm);
        }
        else
        {
            productvm.Product = unit.Product.Get(u => u.Id == id, includeProperties: "ProductImages");
            return View(productvm);
        }
    }

    [HttpPost]
    public IActionResult Upsert([Bind(Prefix = "Product")] Product product, IEnumerable<IFormFile>? files)
    {
        if (ModelState.IsValid)
        {
            if (product.Id == 0)
            {
                unit.Product.Add(product);
                TempData["success"] = "Product created Successfully";
            }

            else
            {
                unit.Product.Update(product);
                TempData["success"] = "Product updated Successfully";

            }
            unit.Save();


            string wwwRootPath = webHost.WebRootPath;

            if (files != null)
            {

                foreach (var file in files)
                {
                    string filename = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = @"images\products\product~" + product.Id;
                    string finalPath = Path.Combine(wwwRootPath, productPath);

                    if (!Directory.Exists(finalPath))
                    {
                        Directory.CreateDirectory(finalPath);
                    }

                    using var fileStream = new FileStream(Path.Combine(finalPath, filename), FileMode.Create);
                    file.CopyTo(fileStream);

                    var productImage = new ProductImage()
                    {
                        ImageUrl = @"\" + productPath + @"\" + filename,
                        ProductId = product.Id,
                    };

                    if (product.ProductImages == null)
                    {
                        product.ProductImages = new();
                    }
                    product.ProductImages.Add(productImage);

                }

                unit.Product.Update(product);
                unit.Save();

                return RedirectToAction("Index");
            }
            else
            {
                if (product.Id == 0)
                {
                    TempData["error"] = "Error creating product";
                }
                else
                {
                    TempData["error"] = "Error updating product";
                }

                IEnumerable<SelectListItem> CategoryList = unit.Category.GetAll().Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.Id.ToString()
                });

                ProductVM productvm = new()
                {
                    Product = new Product(),
                    CategoryList = CategoryList,
                };

                return View(productvm);

            }
        }

        IEnumerable<SelectListItem> Categorylist = unit.Category.GetAll().Select(u => new SelectListItem
        {
            Text = u.Name,
            Value = u.Id.ToString()
        });

        ProductVM productVM = new()
        {
            Product = new Product(),
            CategoryList = Categorylist,
        };

        return View(productVM);
    }

    public IActionResult DeleteImage(int imageId)
    {
        var image = unit.ProductImage.Get(u => u.Id == imageId);
        var productid = image.ProductId;
        if (image != null)
        {
            if (!string.IsNullOrEmpty(image.ImageUrl))
            {
                var oldImagePath = Path.Combine(webHost.WebRootPath,
                    image.ImageUrl.TrimStart('\\'));

                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }
            }

            unit.ProductImage.Remove(image);
            unit.Save();

            TempData["success"] = "Image deleted Successfully";
        }

        return RedirectToAction(nameof(Upsert), new { id = productid });
    }

    #region APICALLS
    [HttpGet]
    public IActionResult GetAll()
    {
        var products = unit.Product.GetAll(includeProperties: "Category");
        return Json(new { data = products });
    }


    [HttpDelete]
    public IActionResult Delete(int? id)
    {
        var product = unit.Product.Get(u => u.Id == id);
        if (product == null)
        {
            return Json(new { success = false, message = "Error while deleting" });
        }


        string productPath = @"images\products\product~" + id;
        string finalPath = Path.Combine(webHost.WebRootPath, productPath);

        if (Directory.Exists(finalPath))
        {
            var files = Directory.GetFiles(finalPath);
            foreach (var file in files)
            {
                System.IO.File.Delete(file);
            }
            Directory.Delete(finalPath);
        }

        unit.Product.Remove(product);
        unit.Save();

        return Json(new { success = true, message = "Deleted Successfully" });

    }
    #endregion
}
