using System;
using System.Linq;
using System.Threading.Tasks;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using AllHoursCafe.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllHoursCafe.API.Controllers
{
    [Authorize]
    public class AddressController : Controller
    {
        private readonly ISavedAddressService _savedAddressService;
        private readonly ILogger<AddressController> _logger;
        private readonly ApplicationDbContext _context;

        public AddressController(ISavedAddressService savedAddressService, ApplicationDbContext context, ILogger<AddressController> logger)
        {
            _savedAddressService = savedAddressService;
            _context = context;
            _logger = logger;
        }

        // GET: /Address
        public async Task<IActionResult> Index()
        {
            try
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Retrieving saved addresses for user: {Email}", userEmail);

                // Get the user directly from the database
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
                if (user != null)
                {
                    _logger.LogInformation("Found user with ID: {UserId}, Email: {Email}", user.Id, user.Email);

                    // Get addresses directly from the database
                    var addresses = await _context.SavedAddresses
                        .Where(a => a.UserId == user.Id)
                        .OrderByDescending(a => a.IsDefault)
                        .ThenByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                        .ToListAsync();

                    _logger.LogInformation("Found {Count} addresses for user ID: {UserId}", addresses.Count, user.Id);

                    // New users should start with no addresses
                    if (addresses.Count == 0)
                    {
                        _logger.LogInformation("No addresses found for user ID {UserId}", user.Id);
                        // No automatic address creation or matching - users must add their own addresses
                    }

                    return View(addresses);
                }
                else
                {
                    _logger.LogWarning("User not found with email: {Email}", userEmail);
                    return View(new System.Collections.Generic.List<SavedAddress>());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving saved addresses");
                TempData["AddressError"] = "An error occurred while retrieving your saved addresses.";
                return View(new System.Collections.Generic.List<SavedAddress>());
            }
        }

        // POST: /Address/SetDefault/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id)
        {
            try
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Setting address ID {AddressId} as default for user: {Email}", id, userEmail);

                var address = await _savedAddressService.GetSavedAddressAsync(userEmail, id);
                if (address == null)
                {
                    _logger.LogWarning("Address ID {AddressId} not found for user: {Email}", id, userEmail);
                    TempData["AddressError"] = "Address not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Set as default
                address.IsDefault = true;
                var result = await _savedAddressService.UpdateSavedAddressAsync(userEmail, address);

                if (result != null)
                {
                    _logger.LogInformation("Successfully set address ID {AddressId} as default", id);
                    TempData["AddressSuccess"] = "Default address updated successfully.";
                }
                else
                {
                    _logger.LogWarning("Failed to set address ID {AddressId} as default", id);
                    TempData["AddressError"] = "Failed to update default address.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default address ID: {AddressId}", id);
                TempData["AddressError"] = "An error occurred while updating your default address.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /Address/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Deleting address ID {AddressId} for user: {Email}", id, userEmail);

                var result = await _savedAddressService.DeleteSavedAddressAsync(userEmail, id);

                if (result)
                {
                    _logger.LogInformation("Successfully deleted address ID {AddressId}", id);
                    TempData["AddressSuccess"] = "Address deleted successfully.";
                }
                else
                {
                    _logger.LogWarning("Failed to delete address ID {AddressId}", id);
                    TempData["AddressError"] = "Failed to delete address.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting address ID: {AddressId}", id);
                TempData["AddressError"] = "An error occurred while deleting your address.";
                return RedirectToAction(nameof(Index));
            }
        }














    }
}
