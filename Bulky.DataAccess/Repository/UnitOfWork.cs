using Bulky.DataAccess.Data;
using Bulky.DataAccess.Repository.IRepository;

namespace Bulky.DataAccess.Repository;

public class UnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public ICategoryRepository Category { get; private set; } = new CategoryRepository(db);
    public IProductRepository Product { get; private set; } = new ProductRepository(db);
    public ICompanyRepository Company { get; private set; } = new CompanyRepository(db);
    public IShoppingCartRepository ShoppingCart { get; private set; } = new ShoppingCartRepository(db);
    public IApplicationUserRepository ApplicationUser { get; private set; } = new ApplicationUserRepository(db);
    public IOrderDetailRepository OrderDetail { get; private set; } = new OrderDetailRepository(db);
    public IOrderHeaderRepository OrderHeader { get; private set; } = new OrderHeaderRepository(db);
    public IProductImageRepository ProductImage { get; private set; } = new ProductImageRepository(db);

    public void Save()
    {
        db.SaveChanges();
    }
}
