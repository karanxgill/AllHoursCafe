using System.ComponentModel.DataAnnotations;

namespace AllHoursCafe.API.Models
{
    public class ReservationPaymentViewModel
    {
        public int ReservationId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public decimal Amount { get; set; } = 500m;
        public string PaymentMethod { get; set; } = "PayU";
        public string PaymentStatus { get; set; } = "Pending";
    }
}
