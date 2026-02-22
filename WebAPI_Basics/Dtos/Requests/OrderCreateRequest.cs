using System.ComponentModel.DataAnnotations;

namespace WebAPI_Basics.Dtos.Requests;

public class OrderCreateRequest
{
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }
}