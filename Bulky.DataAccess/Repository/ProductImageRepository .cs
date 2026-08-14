using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;

namespace Bulky.DataAccess.Repository;

public class ProductImageRepository(ApplicationDbContext db) : Repository<ProductImage>(db), IProductImageRepository
{
    public void Update(ProductImage productimage)
    {
        db.ProductImages.Update(productimage);
    }
}
