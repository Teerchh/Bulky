using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;

namespace Bulky.DataAccess.Repository;

public class ProductRepository(ApplicationDbContext db) : Repository<Product>(db), IProductRepository
{
    public void Update(Product product)
    {
        var productindb = db.Products.FirstOrDefault(x => x.Id == product.Id);
        if (productindb != null)
        {
            productindb.Title = product.Title;
            productindb.Description = product.Description;
            productindb.ISBN = product.ISBN;
            productindb.CategoryId = product.CategoryId;
            productindb.ListPrice = product.ListPrice;
            productindb.Price = product.Price;
            productindb.Price50 = product.Price50;
            productindb.Price100 = product.Price100;
            productindb.Author = product.Author;
            productindb.ProductImages = product.ProductImages;
        }
    }
}
