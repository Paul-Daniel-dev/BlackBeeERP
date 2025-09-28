using BlackBeeERP.Data;
using BlackBeeERP.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackBeeERP.Services;

public class CustomerService
{
    private readonly ErpDbContext _context;

    public CustomerService(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<List<Customer>> GetAllAsync()
    {
        return await _context.Customers.ToListAsync();
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        return await _context.Customers.FindAsync(id);
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task UpdateAsync(Customer customer)
    {
        var existingCustomer = await _context.Customers.FindAsync(customer.Id);
        if (existingCustomer != null)
        {
            _context.Entry(existingCustomer).State = EntityState.Detached;
        }

        _context.Entry(customer).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer != null)
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
        }
    }
}