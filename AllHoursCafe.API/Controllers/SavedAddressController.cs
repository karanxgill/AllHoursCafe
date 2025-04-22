using System;
using System.Threading.Tasks;
using System.Linq;
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
    [Route("api/[controller]")]
    [ApiController]
    public class SavedAddressController : ControllerBase
    {
        private readonly ISavedAddressService _savedAddressService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SavedAddressController> _logger;

        public SavedAddressController(ISavedAddressService savedAddressService, ApplicationDbContext context, ILogger<SavedAddressController> logger)
        {
            _savedAddressService = savedAddressService;
            _context = context;
            _logger = logger;
        }

        // GET: api/SavedAddress
        [HttpGet]
        public async Task<IActionResult> GetSavedAddresses()
        {
            try
            {
                var userEmail = User.Identity.Name;
                var addresses = await _savedAddressService.GetSavedAddressesAsync(userEmail);
                return Ok(addresses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved addresses");
                return StatusCode(500, "An error occurred while retrieving saved addresses");
            }
        }

        // GET: api/SavedAddress/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSavedAddress(int id)
        {
            try
            {
                var userEmail = User.Identity.Name;
                var addresses = await _savedAddressService.GetSavedAddressesAsync(userEmail);
                var address = addresses.Find(a => a.Id == id);

                if (address == null)
                {
                    return NotFound();
                }

                return Ok(address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting saved address with ID: {AddressId}", id);
                return StatusCode(500, "An error occurred while retrieving the saved address");
            }
        }

        // POST: api/SavedAddress
        [HttpPost]
        public async Task<IActionResult> CreateSavedAddress(SavedAddress address)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userEmail = User.Identity.Name;
                var createdAddress = await _savedAddressService.CreateSavedAddressAsync(userEmail, address);

                if (createdAddress == null)
                {
                    return BadRequest("Failed to create saved address");
                }

                return CreatedAtAction(nameof(GetSavedAddress), new { id = createdAddress.Id }, createdAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating saved address");
                return StatusCode(500, "An error occurred while creating the saved address");
            }
        }

        // PUT: api/SavedAddress/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSavedAddress(int id, SavedAddress address)
        {
            try
            {
                if (id != address.Id)
                {
                    return BadRequest("ID mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userEmail = User.Identity.Name;
                var updatedAddress = await _savedAddressService.UpdateSavedAddressAsync(userEmail, address);

                if (updatedAddress == null)
                {
                    return NotFound();
                }

                return Ok(updatedAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating saved address with ID: {AddressId}", id);
                return StatusCode(500, "An error occurred while updating the saved address");
            }
        }

        // DELETE: api/SavedAddress/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSavedAddress(int id)
        {
            try
            {
                var userEmail = User.Identity.Name;
                var result = await _savedAddressService.DeleteSavedAddressAsync(userEmail, id);

                if (!result)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting saved address with ID: {AddressId}", id);
                return StatusCode(500, "An error occurred while deleting the saved address");
            }
        }

        // POST: api/SavedAddress/{id}/SetDefault
        [HttpPost("{id}/SetDefault")]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            try
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Setting address ID {AddressId} as default for user: {Email}", id, userEmail);

                // Get the address
                var addresses = await _savedAddressService.GetSavedAddressesAsync(userEmail);
                var address = addresses.Find(a => a.Id == id);

                if (address == null)
                {
                    _logger.LogWarning("Address ID {AddressId} not found for user: {Email}", id, userEmail);
                    return NotFound("Address not found");
                }

                // Set as default
                address.IsDefault = true;
                var result = await _savedAddressService.UpdateSavedAddressAsync(userEmail, address);

                if (result == null)
                {
                    _logger.LogWarning("Failed to set address ID {AddressId} as default", id);
                    return StatusCode(500, "Failed to update default address");
                }

                _logger.LogInformation("Successfully set address ID {AddressId} as default", id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default address ID: {AddressId}", id);
                return StatusCode(500, "An error occurred while updating your default address");
            }
        }

        // POST: api/SavedAddress/FromOrder/{orderId}
        [HttpPost("FromOrder/{orderId}")]
        public async Task<IActionResult> CreateFromOrder(int orderId, [FromQuery] string name)
        {
            try
            {
                var userEmail = User.Identity.Name;

                // Get the order from the database
                var order = await _context.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return NotFound("Order not found");
                }

                // Check if the order belongs to the current user
                if (order.CustomerEmail.ToLower() != userEmail.ToLower())
                {
                    return Forbid();
                }

                // Create a saved address from the order
                var address = await _savedAddressService.CreateFromOrderAsync(userEmail, order, name);

                if (address == null)
                {
                    return BadRequest("Failed to create saved address from order");
                }

                return CreatedAtAction(nameof(GetSavedAddress), new { id = address.Id }, address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating saved address from order with ID: {OrderId}", orderId);
                return StatusCode(500, "An error occurred while creating the saved address from the order");
            }
        }

        // POST: api/SavedAddress/FromForm
        [HttpPost("FromForm")]
        [AllowAnonymous] // Allow unauthenticated users to save addresses
        public async Task<IActionResult> CreateFromForm([FromBody] SaveAddressRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state when creating saved address from form: {Errors}",
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                    return BadRequest(new { message = "Please fill in all required fields." });
                }

                // Get user email from authentication if available
                var userEmail = User.Identity?.Name;
                _logger.LogInformation("User authentication status: {IsAuthenticated}", User.Identity?.IsAuthenticated);
                _logger.LogInformation("User email from Identity: {Email}", userEmail);

                // If user is not authenticated, use the email from the request
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogInformation("User not authenticated, using email from request");
                    // Check if we have a session ID or cookie we can use to identify the user
                    // For now, we'll use a temporary solution
                    userEmail = request.CustomerName.Replace(" ", "") + "@temp.com";
                    _logger.LogInformation("Generated temporary email: {Email}", userEmail);
                }

                _logger.LogInformation("Creating saved address from form for user: {Email}", userEmail);

                // Create a new saved address from the form data
                var address = new SavedAddress
                {
                    Name = request.AddressName ?? "My Address",
                    CustomerName = request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    DeliveryAddress = request.DeliveryAddress,
                    City = request.City,
                    State = request.State,
                    PostalCode = request.PostalCode,
                    IsDefault = request.IsDefault
                };

                var savedAddress = await _savedAddressService.CreateSavedAddressAsync(userEmail, address);

                if (savedAddress == null)
                {
                    _logger.LogWarning("Failed to create saved address for user: {Email}", userEmail);
                    return BadRequest(new { message = "Failed to create saved address. Please try again." });
                }

                _logger.LogInformation("Successfully created saved address with ID: {AddressId} for user: {Email}", savedAddress.Id, userEmail);
                return CreatedAtAction(nameof(GetSavedAddress), new { id = savedAddress.Id }, savedAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating saved address from form");
                return StatusCode(500, new { message = "An error occurred while creating the saved address" });
            }
        }
    }
}
