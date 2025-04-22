using System.ComponentModel.DataAnnotations;

namespace AllHoursCafe.API.Models
{
    public class SaveAddressRequest
    {
        // Optional email for unauthenticated users
        public string? Email { get; set; }
        [Required(ErrorMessage = "Customer name is required")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        public string CustomerPhone { get; set; }

        [Required(ErrorMessage = "Delivery address is required")]
        public string DeliveryAddress { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; }

        [Required(ErrorMessage = "State is required")]
        public string State { get; set; }

        [Required(ErrorMessage = "Postal code is required")]
        public string PostalCode { get; set; }

        public string? AddressName { get; set; }

        public bool IsDefault { get; set; } = false;
    }
}
