using BlackBeeERP.Data;
using BlackBeeERP.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackBeeERP.Services;

public class OrderService
{
    private readonly ErpDbContext _context;

    public OrderService(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<List<Order>> GetAllAsync()
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .ToListAsync();
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order> CreateAsync(Order order)
    {
        try
        {
            _context.ChangeTracker.Clear();

            // Ensure OrderDate is in UTC
            if (order.OrderDate.Kind != DateTimeKind.Utc)
            {
                order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
            }

            // Validate stock quantities
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product with ID {item.ProductId} not found");
                }

                if (product.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Not enough stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}");
                }
            }

            // Calculate total amount
            order.TotalAmount = order.OrderItems.Sum(item => item.Quantity * item.UnitPrice);

            // First add the order without items
            var orderOnly = new Order
            {
                CustomerId = order.CustomerId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                OrderItems = new List<OrderItem>() // Empty list
            };

            _context.Orders.Add(orderOnly);
            await _context.SaveChangesAsync();

            // Now add the order items separately with the new order ID
            foreach (var item in order.OrderItems)
            {
                var orderItem = new OrderItem
                {
                    OrderId = orderOnly.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                };

                _context.OrderItems.Add(orderItem);

                // Update the product stock quantity
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                    _context.Products.Update(product);
                }
            }

            await _context.SaveChangesAsync();

            // Return the complete order
            return await GetByIdAsync(orderOnly.Id) ?? orderOnly;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(Order order)
    {
        try
        {
            _context.ChangeTracker.Clear();

            // Get existing order with items to restore stock
            var existingOrder = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (existingOrder == null)
            {
                throw new InvalidOperationException($"Order with ID {order.Id} not found");
            }

            // Ensure OrderDate is in UTC
            if (order.OrderDate.Kind != DateTimeKind.Utc)
            {
                order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
            }

            // Handle special case for status change only - to avoid stock issues when just changing status
            bool isStatusChangeOnly =
                existingOrder.OrderItems.Count == order.OrderItems.Count &&
                order.CustomerId == existingOrder.CustomerId &&
                order.Status != existingOrder.Status &&
                !order.OrderItems.Select(i => i.ProductId).Except(existingOrder.OrderItems.Select(i => i.ProductId)).Any();

            if (isStatusChangeOnly)
            {
                // Just update the status without touching inventory
                existingOrder.Status = order.Status;
                await _context.SaveChangesAsync();
                return;
            }

            // Regular update flow with inventory changes
            // Restore product quantities from the existing order items
            foreach (var existingItem in existingOrder.OrderItems)
            {
                var product = await _context.Products.FindAsync(existingItem.ProductId);
                if (product != null)
                {
                    product.StockQuantity += existingItem.Quantity;
                    _context.Products.Update(product);
                }
            }

            // Now validate the new quantities
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product with ID {item.ProductId} not found");
                }

                if (product.StockQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Not enough stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}");
                }
            }

            // Handle order items separately
            var existingItems = await _context.OrderItems
                .Where(oi => oi.OrderId == order.Id)
                .ToListAsync();

            // Remove existing items
            _context.OrderItems.RemoveRange(existingItems);
            await _context.SaveChangesAsync(); // Save changes to remove items first

            _context.ChangeTracker.Clear(); // Clear again to be safe

            var orderEntity = await _context.Orders.FindAsync(order.Id);
            if (orderEntity != null)
            {
                orderEntity.CustomerId = order.CustomerId;
                orderEntity.OrderDate = order.OrderDate;
                orderEntity.Status = order.Status;
                orderEntity.TotalAmount = order.OrderItems.Sum(item => item.Quantity * item.UnitPrice);

                await _context.SaveChangesAsync(); // Save the order update
            }

            // Now add the order items separately
            foreach (var item in order.OrderItems)
            {
                // Create new items with clean IDs to avoid conflicts
                var newItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                };

                _context.OrderItems.Add(newItem);

                // Update the product stock quantity
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                    _context.Products.Update(product);
                }
            }

            await _context.SaveChangesAsync(); // Save the new items
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            _context.ChangeTracker.Clear();

            // Get the order with items to restore stock
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return; // Nothing to delete
            }

            // Restore product quantities
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    _context.Products.Update(product);
                }
            }

            // Remove the order items
            _context.OrderItems.RemoveRange(order.OrderItems);

            // Remove the order
            _context.Orders.Remove(order);

            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Order>> GetRecentOrdersAsync(int count = 5)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalSalesAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Orders.AsQueryable();

        // Ensure dates are in UTC
        if (startDate.HasValue && startDate.Value.Kind != DateTimeKind.Utc)
        {
            startDate = DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc);
        }

        if (endDate.HasValue && endDate.Value.Kind != DateTimeKind.Utc)
        {
            endDate = DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc);
        }

        if (startDate.HasValue)
            query = query.Where(o => o.OrderDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(o => o.OrderDate <= endDate.Value);

        return await query.SumAsync(o => o.TotalAmount);
    }
}