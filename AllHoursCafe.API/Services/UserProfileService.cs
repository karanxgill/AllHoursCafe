using System;
using System.Linq;
using System.Threading.Tasks;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllHoursCafe.API.Services
{
    public class UserProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserProfileService> _logger;

        public UserProfileService(ApplicationDbContext context, ILogger<UserProfileService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets the user profile information, including contact details from the most recent order if available
        /// </summary>
        public async Task<UserProfileInfo> GetUserProfileInfoAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Email is null or empty when getting user profile info");
                return null;
            }

            _logger.LogInformation("Getting user profile info for email: {Email}", email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return null;
            }

            _logger.LogInformation("Found user with ID: {UserId}", user.Id);

            // Get the most recent order for this user
            var lastOrder = await _context.Orders
                .Where(o => o.CustomerEmail.ToLower() == email.ToLower())
                .OrderByDescending(o => o.OrderDate)
                .FirstOrDefaultAsync();

            // Create the profile info object
            var profileInfo = new UserProfileInfo
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                City = user.City,
                State = user.State,
                PostalCode = user.PostalCode
            };

            // If we have a last order, update any missing fields in the user profile
            if (lastOrder != null)
            {
                _logger.LogInformation("Found last order with ID: {OrderId}", lastOrder.Id);

                // Update the user profile with order information if it's missing
                bool userUpdated = false;

                if (string.IsNullOrEmpty(user.PhoneNumber) && !string.IsNullOrEmpty(lastOrder.CustomerPhone))
                {
                    user.PhoneNumber = lastOrder.CustomerPhone;
                    profileInfo.PhoneNumber = lastOrder.CustomerPhone;
                    userUpdated = true;
                }

                if (string.IsNullOrEmpty(user.Address) && !string.IsNullOrEmpty(lastOrder.DeliveryAddress))
                {
                    user.Address = lastOrder.DeliveryAddress;
                    profileInfo.Address = lastOrder.DeliveryAddress;
                    userUpdated = true;
                }

                if (string.IsNullOrEmpty(user.City) && !string.IsNullOrEmpty(lastOrder.City))
                {
                    user.City = lastOrder.City;
                    profileInfo.City = lastOrder.City;
                    userUpdated = true;
                }

                if (string.IsNullOrEmpty(user.State) && !string.IsNullOrEmpty(lastOrder.State))
                {
                    user.State = lastOrder.State;
                    profileInfo.State = lastOrder.State;
                    userUpdated = true;
                }

                if (string.IsNullOrEmpty(user.PostalCode) && !string.IsNullOrEmpty(lastOrder.PostalCode))
                {
                    user.PostalCode = lastOrder.PostalCode;
                    profileInfo.PostalCode = lastOrder.PostalCode;
                    userUpdated = true;
                }

                // If we updated the user, save the changes
                if (userUpdated)
                {
                    _logger.LogInformation("Updating user profile with information from last order");
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("User profile updated with order information");
                }

                // Use the order information for any fields that are still missing
                if (string.IsNullOrEmpty(profileInfo.PhoneNumber))
                {
                    profileInfo.PhoneNumber = lastOrder.CustomerPhone;
                }

                if (string.IsNullOrEmpty(profileInfo.Address))
                {
                    profileInfo.Address = lastOrder.DeliveryAddress;
                }

                if (string.IsNullOrEmpty(profileInfo.City))
                {
                    profileInfo.City = lastOrder.City;
                }

                if (string.IsNullOrEmpty(profileInfo.State))
                {
                    profileInfo.State = lastOrder.State;
                }

                if (string.IsNullOrEmpty(profileInfo.PostalCode))
                {
                    profileInfo.PostalCode = lastOrder.PostalCode;
                }
            }

            return profileInfo;
        }

        /// <summary>
        /// Updates the user profile with the provided information
        /// </summary>
        public async Task UpdateUserProfileAsync(string email, Order order)
        {
            if (string.IsNullOrEmpty(email) || order == null)
            {
                _logger.LogWarning("Email or order is null when updating user profile");
                return;
            }

            _logger.LogInformation("Updating user profile for email: {Email}", email);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return;
            }

            _logger.LogInformation("Found user with ID: {UserId}", user.Id);

            // Update the user profile with order information
            user.PhoneNumber = order.CustomerPhone;
            user.Address = order.DeliveryAddress;
            user.City = order.City;
            user.State = order.State;
            user.PostalCode = order.PostalCode;

            _logger.LogInformation("Updated user fields - Phone: {Phone}, Address: {Address}, City: {City}, State: {State}, PostalCode: {PostalCode}",
                user.PhoneNumber, user.Address, user.City, user.State, user.PostalCode);

            // Save the changes to the database
            _context.Update(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User profile updated in database");
        }
    }

    public class UserProfileInfo
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
    }
}
