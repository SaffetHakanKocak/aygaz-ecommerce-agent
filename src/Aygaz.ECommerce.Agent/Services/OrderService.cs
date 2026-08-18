using System.Linq.Expressions;
using Aygaz.ECommerce.Agent.Data;
using Aygaz.ECommerce.Agent.Entities;
using Aygaz.ECommerce.Agent.Models;
using Microsoft.EntityFrameworkCore;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OrderService(ECommerceDbContext dbContext) : IOrderService
{
    private static readonly Expression<Func<CustomerOrder, OrderDto>> ToDto = order =>
        new OrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.OrderDate,
            order.Status,
            order.TotalAmount);

    public Task<OrderDto?> GetOrderByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return Task.FromResult<OrderDto?>(null);
        }

        return dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.Id == id)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<OrderDto?> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return Task.FromResult<OrderDto?>(null);
        }

        string normalizedOrderNumber = orderNumber.Trim();

        return dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.OrderNumber == normalizedOrderNumber)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0 || maxResults <= 0)
        {
            return Array.Empty<OrderDto>();
        }

        return await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .Take(maxResults)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    public Task<OrderDto?> GetLatestCustomerOrderAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return Task.FromResult<OrderDto?>(null);
        }

        return dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
