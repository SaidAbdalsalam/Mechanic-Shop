using System.Net.Mail;
using System.Text.RegularExpressions;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Customers;

public sealed class Customer : AuditableEntity
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public string Address { get; private set; }
    private List<Vehicle> _vehicles = [];
    public IReadOnlyList<Vehicle> Vehicles => _vehicles.AsReadOnly();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private Customer() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Customer(
        Guid id,
        string name,
        string email,
        string phoneNumber,
        string address,
        List<Vehicle> vehicles
    )
        : base(id)
    {
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        _vehicles = [.. vehicles];
    }

    public static Result<Customer> Create(
        Guid id,
        string name,
        string email,
        string phoneNumber,
        string address,
        List<Vehicle> vehicles
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            return CustomerErrors.AddressRequired;
        }

        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return CustomerErrors.EmailInvalid;
        }

        return new Customer(id, name, email, phoneNumber, address, vehicles);
    }

    public Result<Updated> AddVehicle(Vehicle vehicle)
    {
        _vehicles.Add(vehicle);
        return Result.Updated;
    }

    public Result<Updated> Update(string name, string phoneNumber, string email, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CustomerErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return CustomerErrors.EmailRequired;
        }
        try
        {
            _ = new MailAddress(email);
        }
        catch
        {
            return CustomerErrors.EmailInvalid;
        }
        if (string.IsNullOrWhiteSpace(phoneNumber) || !Regex.IsMatch(phoneNumber, @"^\+?\d{7,15}$"))
        {
            return CustomerErrors.InvalidPhoneNumber;
        }
        if (string.IsNullOrWhiteSpace(address))
        {
            return CustomerErrors.AddressRequired;
        }
        Name = name;
        Email = email;
        PhoneNumber = phoneNumber;
        Address = address;
        return Result.Updated;
    }
}
