using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;

public sealed class Employee : AuditableEntity
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string FullName => $"{FirstName} {LastName}";
    public Role Role { get; private set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Employee() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Employee(Guid id, string firstName, string lastName, Role role)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Role = role;
    }

    public static Result<Employee> Create(Guid id, string firstName, string lastName, Role role)
    {
        if (id == Guid.Empty)
            return EmployeeErrors.IdRequired;
        if (string.IsNullOrWhiteSpace(firstName))
            return EmployeeErrors.FirstNameRequired;
        if (string.IsNullOrWhiteSpace(lastName))
            return EmployeeErrors.LastNameRequired;
        if (!Enum.IsDefined(role))
            return EmployeeErrors.RoleInvalid;

        return new Employee(id, firstName.Trim(), lastName.Trim(), role);
    }
}
