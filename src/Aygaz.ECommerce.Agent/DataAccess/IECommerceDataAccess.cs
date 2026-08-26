using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.DataAccess;

public interface IECommerceDataAccess
{
    Task<CustomerDto?> GetCustomerByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetCustomerByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> SearchCustomersByNameAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> SearchCustomersByCityAsync(
        string city,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> GetAllCustomersAsync(
        CancellationToken cancellationToken = default);

    Task<ProductDto?> GetProductByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<ProductDto?> GetProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> SearchProductsAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderDto>> GetCustomerOrdersAsync(
        int customerId,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetLatestCustomerOrderAsync(
        int customerId,
        CancellationToken cancellationToken = default);

    Task<OrderDetailDto?> GetCustomerOrderDetailAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryDto>> GetProductInventoryAsync(
        int productId,
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<long?> GetTotalAvailableStockAsync(
        int productId,
        CancellationToken cancellationToken = default);

    Task<SalesSummaryDto> GetSalesSummaryAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopSellingProductDto>> GetTopSellingProductsAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CustomerPurchaseSummaryDto?> GetCustomerPurchaseSummaryAsync(
        int customerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}
