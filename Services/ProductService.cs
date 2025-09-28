using BlackBeeERP.Data;
using BlackBeeERP.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackBeeERP.Services;

public class ProductService
{
    private readonly ErpDbContext _context;

    public ProductService(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _context.Products.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products.FindAsync(id);
    }

    public async Task<Product> CreateAsync(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    public async Task UpdateAsync(Product product)
    {
        try
        {
            // Completely detach all tracked entities to avoid conflicts
            _context.ChangeTracker.Clear();

            // Now attach and update the product
            _context.Entry(product).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw; 
        }
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Product>> GetLowStockProductsAsync(int threshold = 10)
    {
        return await _context.Products
            .Where(p => p.StockQuantity < threshold)
            .ToListAsync();
    }
}