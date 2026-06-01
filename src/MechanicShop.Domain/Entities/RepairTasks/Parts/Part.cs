using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Parts;

namespace MechanicShop.Domain.RepairTasks;

public sealed class Part : AuditableEntity
{
    public string Name { get; private set; }

    public decimal Cost { get; private set; }

    public int Quantity { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Part() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Part(Guid id, string name, decimal cost, int quantity)
        : base(id)
    {
        Name = name;
        Cost = cost;
        Quantity = quantity;
    }

    public static Result<Part> Create(Guid id, string name, decimal cost, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PartErrors.NameRequired;
        }

        if (cost <= 0 || cost > 10000)
        {
            return PartErrors.CostInvalid;
        }

        if (quantity <= 0 || quantity > 10)
        {
            return PartErrors.QuantityInvalid;
        }

        return new Part(id, name.Trim(), cost, quantity);
    }

    public Result<Updated> Update(string name, decimal cost, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PartErrors.NameRequired;
        }

        if (cost <= 0 || cost > 10000)
        {
            return PartErrors.CostInvalid;
        }

        if (quantity <= 0 || quantity > 10)
        {
            return PartErrors.QuantityInvalid;
        }

        Name = name.Trim();
        Cost = cost;
        Quantity = quantity;

        return Result.Updated;
    }
}
