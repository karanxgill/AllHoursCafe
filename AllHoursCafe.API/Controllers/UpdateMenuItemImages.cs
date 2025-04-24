using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AllHoursCafe.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UpdateMenuItemImagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UpdateMenuItemImagesController> _logger;

        public UpdateMenuItemImagesController(ApplicationDbContext context, ILogger<UpdateMenuItemImagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/UpdateMenuItemImages
        [HttpGet]
        public async Task<IActionResult> UpdateImages()
        {
            try
            {
                // Define the menu items to update with their new image paths
                var itemsToUpdate = new Dictionary<string, string>
                {
                    // Dessert items
                    { "Chocolate Brownie", "/images/Items/dessert/chocolate-brownie.jpg" },
                    { "New York Cheesecake", "/images/Items/dessert/new-york-cheesecake.jpg" },
                    { "Fruit Tart", "/images/Items/dessert/fruit-tart.jpg" },
                    
                    // Breakfast items
                    { "Avocado Toast", "/images/Items/breakfast/avocado-toast.jpg" },
                    { "Breakfast Burrito", "/images/Items/breakfast/breakfast-burrito.jpg" },
                    { "Classic Pancakes", "/images/Items/breakfast/fluffy-pancakes.jpg" }
                };

                int updatedCount = 0;

                // Update each menu item
                foreach (var item in itemsToUpdate)
                {
                    var menuItem = await _context.MenuItems
                        .FirstOrDefaultAsync(m => m.Name == item.Key);

                    if (menuItem != null)
                    {
                        _logger.LogInformation($"Updating image URL for {menuItem.Name} from {menuItem.ImageUrl} to {item.Value}");
                        menuItem.ImageUrl = item.Value;
                        updatedCount++;
                    }
                    else
                    {
                        _logger.LogWarning($"Menu item not found: {item.Key}");
                    }
                }

                // Save changes to the database
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully updated {updatedCount} menu item image URLs");

                return Ok(new { message = $"Successfully updated {updatedCount} menu item image URLs" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu item image URLs");
                return StatusCode(500, new { message = "An error occurred while updating menu item image URLs", error = ex.Message });
            }
        }
    }
}
