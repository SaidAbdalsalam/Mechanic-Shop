using System.ComponentModel.DataAnnotations;

namespace MechanicShop.Api.Requests.WorkOrders;

public class AssignLaborRequest
{
    [Required(ErrorMessage = "LaborId is required.")]
    public string LaborId { get; set; } = string.Empty;
}
