using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.WorkOrders;

namespace MechanicShop.Domain.Invoices;

public sealed class Invoice : AuditableEntity
{
    public Guid WorkOrderId { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Unpaid;
    private readonly List<InvoiceLineItem> _lineItems = [];
    public IReadOnlyList<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();
    public decimal Subtotal => LineItems.Sum(x => x.LineTotal);
    public decimal TaxRate { get; private set; }
    public decimal? DiscountAmount { get; private set; }
    public WorkOrder? WorkOrder { get; private set; }
    public decimal SubtotalAfterDiscount => Subtotal - (DiscountAmount ?? 0m);
    public decimal TaxAmount => SubtotalAfterDiscount * TaxRate;
    public decimal Total => SubtotalAfterDiscount + TaxAmount;
    public decimal ActualLaborCost { get; private set; }
    public decimal ActualPartsCost { get; private set; }

    private Invoice() { }

    private Invoice(
        Guid id,
        Guid workOrderId,
        DateTimeOffset issuedAt,
        List<InvoiceLineItem> lineItems,
        decimal discountAmount,
        decimal taxRate,
        decimal laborCost,
        decimal partsCost
    )
        : base(id)
    {
        WorkOrderId = workOrderId;
        IssuedAtUtc = issuedAt;
        DiscountAmount = discountAmount;
        Status = InvoiceStatus.Unpaid;
        TaxRate = taxRate;
        _lineItems = [.. lineItems];
        ActualLaborCost = laborCost;
        ActualPartsCost = partsCost;
    }

    public static Result<Invoice> Create(
        Guid id,
        Guid workOrderId,
        List<InvoiceLineItem> items,
        decimal discountAmount,
        decimal taxRate,
        TimeProvider datetime,
        decimal laborCost,
        decimal partsCost
    )
    {
        if (workOrderId == Guid.Empty)
        {
            return InvoiceErrors.WorkOrderIdInvalid;
        }

        if (items is null || items.Count == 0)
        {
            return InvoiceErrors.LineItemsEmpty;
        }
        if (taxRate < 0)
        {
            return InvoiceErrors.TaxRateNegative;
        }
        if (laborCost < 0 || partsCost < 0)
        {
            return InvoiceErrors.ActualCostsNegative;
        }

        var invoice = new Invoice(
            id,
            workOrderId,
            datetime.GetUtcNow(),
            items,
            0m,
            taxRate,
            laborCost,
            partsCost
        );
        var result = invoice.ApplyDiscount(discountAmount);
        if (result.IsError)
        {
            return result.Errors;
        }

        return invoice;
    }

    public Result<Updated> ApplyDiscount(decimal discountAmount)
    {
        if (Status != InvoiceStatus.Unpaid)
        {
            return InvoiceErrors.InvoiceLocked;
        }

        if (discountAmount < 0)
        {
            return InvoiceErrors.DiscountNegative;
        }

        if (discountAmount > Subtotal)
        {
            return InvoiceErrors.DiscountExceedsSubtotal;
        }

        DiscountAmount = discountAmount;

        return Result.Updated;
    }

    public Result<Updated> MarkAsPaid(TimeProvider timeProvider)
    {
        if (Status != InvoiceStatus.Unpaid)
        {
            return InvoiceErrors.InvoiceLocked;
        }

        Status = InvoiceStatus.Paid;
        PaidAtUtc = timeProvider.GetUtcNow();

        return Result.Updated;
    }
}
