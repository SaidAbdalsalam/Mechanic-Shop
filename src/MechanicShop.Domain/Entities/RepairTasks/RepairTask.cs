using System.Security.Cryptography.X509Certificates;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Domain.RepairTasks;

public sealed class RepairTask : AuditableEntity
{
    public string Name { get; private set; }
    public RepairDurationInMinutes EstimatedDurationInMins { get; private set; }
    public decimal LaborCost { get; private set; }
    private readonly List<Part> _parts = [];
    public IEnumerable<Part> Parts => _parts.AsReadOnly();
    public decimal TotalCost => LaborCost + Parts.Sum(p => p.Cost * p.Quantity);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private RepairTask() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private RepairTask(
        Guid id,
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost,
        List<Part> parts
    )
        : base(id)
    {
        Name = name;
        EstimatedDurationInMins = estimatedDurationInMins;
        LaborCost = laborCost;
        _parts = parts;
    }

    public static Result<RepairTask> Create(
        Guid id,
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost,
        List<Part> parts
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RepairTaskErrors.NameRequired;
        }

        if (!Enum.IsDefined<RepairDurationInMinutes>(estimatedDurationInMins))
        {
            return RepairTaskErrors.DurationInvalid;
        }

        if (laborCost <= 0)
        {
            return RepairTaskErrors.LaborCostInvalid;
        }
        return new RepairTask(id, name.Trim(), estimatedDurationInMins, laborCost, parts);
    }

    public Result<Updated> Update(
        string name,
        RepairDurationInMinutes estimatedDurationInMins,
        decimal laborCost
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return RepairTaskErrors.NameRequired;
        }

        if (!Enum.IsDefined<RepairDurationInMinutes>(estimatedDurationInMins))
        {
            return RepairTaskErrors.DurationInvalid;
        }

        if (laborCost <= 0)
        {
            return RepairTaskErrors.LaborCostInvalid;
        }
        Name = name.Trim();
        EstimatedDurationInMins = estimatedDurationInMins;
        LaborCost = laborCost;
        return Result.Updated;
    }

    public Result<Updated> UpsertParts(List<Part> incomingParts)
    {
        _parts.RemoveAll(existing => incomingParts.All(p => existing.Id != p.Id));

        foreach (var incoming in incomingParts)
        {
            var existing = _parts.FirstOrDefault(p => p.Id == incoming.Id);
            if (existing is null)
            {
                _parts.Add(incoming);
            }
            else
            {
                var updatePartResult = existing.Update(
                    incoming.Name,
                    incoming.Cost,
                    incoming.Quantity
                );
                if (updatePartResult.IsError)
                {
                    return updatePartResult.Errors;
                }
            }
        }
        return Result.Updated;
    }
}
