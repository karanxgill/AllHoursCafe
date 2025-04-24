using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AllHoursCafe.API.Models
{
    public class MenuHighlight
    {
        public int Id { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [ForeignKey("MenuItemId")]
        public MenuItem? MenuItem { get; set; } // Make nullable to avoid validation

        public int DisplayOrder { get; set; }

        public string Section { get; set; } = "Breakfast"; // Default section (Breakfast, Beverages, etc.)

        [StringLength(100)]
        public string? CustomTitle { get; set; }

        [StringLength(200)]
        public string? CustomDescription { get; set; }

        [StringLength(255)]
        public string? CustomImageUrl { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
