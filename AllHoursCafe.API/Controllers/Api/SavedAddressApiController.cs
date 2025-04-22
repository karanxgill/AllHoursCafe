using System;
using System.Threading.Tasks;
using AllHoursCafe.API.Models;
using AllHoursCafe.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AllHoursCafe.API.Controllers.Api
{
    [Route("api/SavedAddressApi")]
    [ApiController]
    public class SavedAddressApiController : ControllerBase
    {
        private readonly ISavedAddressService _savedAddressService;
        private readonly ILogger<SavedAddressApiController> _logger;

        public SavedAddressApiController(
            ISavedAddressService savedAddressService,
            ILogger<SavedAddressApiController> logger)
        {
            _savedAddressService = savedAddressService;
            _logger = logger;
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

                // If user is not authenticated, try to use the request's email, else create a temporary one
                if (string.IsNullOrEmpty(userEmail))
                {
                    if (!string.IsNullOrEmpty(request.Email))
                    {
                        userEmail = request.Email;
                        _logger.LogInformation("User not authenticated, using email from request: {Email}", userEmail);
                    }
                    else
                    {
                        userEmail = request.CustomerName.Replace(" ", "").ToLower() + "@temp.allhourscafe.com";
                        _logger.LogWarning("User not authenticated and request email missing, using temporary email: {Email}", userEmail);
                    }
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
                return Ok(new {
                    message = "Address saved successfully!",
                    addressId = savedAddress.Id,
                    addressName = savedAddress.Name
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating saved address from form");
                return StatusCode(500, new { message = "An error occurred while creating the saved address" });
            }
        }
    }
}
