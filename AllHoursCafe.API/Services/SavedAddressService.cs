using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllHoursCafe.API.Services
{
    public class SavedAddressService : ISavedAddressService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SavedAddressService> _logger;

        public SavedAddressService(ApplicationDbContext context, ILogger<SavedAddressService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets all saved addresses for a user
        /// </summary>
        public async Task<List<SavedAddress>> GetSavedAddressesAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Email is null or empty when getting saved addresses");
                return new List<SavedAddress>();
            }

            _logger.LogInformation("Getting saved addresses for email: {Email}", email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);

                // Debug: List all users in the database
                var allUsers = await _context.Users.ToListAsync();
                _logger.LogInformation("All users in database: {Count}", allUsers.Count);
                foreach (var u in allUsers)
                {
                    _logger.LogInformation("DB User: ID={Id}, Email={Email}", u.Id, u.Email);
                }

                // Try to find a user with a similar email (case insensitive)
                var similarUser = await _context.Users
                    .FirstOrDefaultAsync(u => EF.Functions.Like(u.Email.ToLower(), $"%{email.ToLower()}%"));

                if (similarUser != null)
                {
                    _logger.LogInformation("Found similar user with email: {Email}", similarUser.Email);
                    user = similarUser;
                }
                else
                {
                    return new List<SavedAddress>();
                }
            }

            _logger.LogInformation("Found user with ID: {UserId} for email: {Email}", user.Id, email);

            // Get all saved addresses for this user
            _logger.LogInformation("Querying saved addresses for user ID: {UserId}", user.Id);

            // Directly query the SavedAddresses table for this user
            List<SavedAddress> addresses = new List<SavedAddress>();

            try {
                // Count total saved addresses in the table
                var totalAddresses = await _context.SavedAddresses.CountAsync();
                _logger.LogInformation("Total saved addresses in the database: {TotalAddresses}", totalAddresses);

                // Debug: Log all addresses in the database
                var allAddresses = await _context.SavedAddresses.ToListAsync();
                _logger.LogInformation("All addresses in database: {Count}", allAddresses.Count);
                foreach (var addr in allAddresses)
                {
                    _logger.LogInformation("DB Address: ID={Id}, UserId={UserId}, Name={Name}, CustomerName={CustomerName}, CustomerPhone={CustomerPhone}, DeliveryAddress={DeliveryAddress}, City={City}, State={State}, PostalCode={PostalCode}, IsDefault={IsDefault}",
                        addr.Id, addr.UserId, addr.Name, addr.CustomerName, addr.CustomerPhone, addr.DeliveryAddress, addr.City, addr.State, addr.PostalCode, addr.IsDefault);
                }

                // Get addresses for this specific user
                addresses = await _context.SavedAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.IsDefault)
                    .ThenByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} addresses for user ID {UserId}", addresses.Count, user.Id);
                foreach (var addr in addresses)
                {
                    _logger.LogInformation("Returned Address for user: ID={Id}, UserId={UserId}, Name={Name}, CustomerName={CustomerName}, CustomerPhone={CustomerPhone}, DeliveryAddress={DeliveryAddress}, City={City}, State={State}, PostalCode={PostalCode}, IsDefault={IsDefault}",
                        addr.Id, addr.UserId, addr.Name, addr.CustomerName, addr.CustomerPhone, addr.DeliveryAddress, addr.City, addr.State, addr.PostalCode, addr.IsDefault);
                }

                // New users should start with no addresses
                if (addresses.Count == 0)
                {
                    _logger.LogInformation("No addresses found for user ID {UserId}", user.Id);
                    // No automatic address creation or matching - users must add their own addresses
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error retrieving saved addresses for user ID {UserId}", user.Id);
                // Return an empty list rather than throwing an exception
                return new List<SavedAddress>();
            }

            _logger.LogInformation("Found {Count} saved addresses for user ID: {UserId}", addresses.Count, user.Id);

            // Log details of each address
            foreach (var address in addresses)
            {
                _logger.LogInformation("Address ID: {Id}, Name: {Name}, Customer: {CustomerName}, Address: {Address}",
                    address.Id, address.Name, address.CustomerName, address.DeliveryAddress);
            }

            return addresses;
        }

        /// <summary>
        /// Creates a new saved address for a user
        /// </summary>
        public async Task<SavedAddress> CreateSavedAddressAsync(string email, SavedAddress address)
        {
            if (string.IsNullOrEmpty(email) || address == null)
            {
                _logger.LogWarning("Email or address is null when creating saved address");
                return null;
            }

            _logger.LogInformation("Creating saved address for email: {Email}", email);
            _logger.LogInformation("Address details: Name={Name}, CustomerName={CustomerName}, Address={Address}",
                address.Name, address.CustomerName, address.DeliveryAddress);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            // If user doesn't exist, do NOT create a new one. Log and return null.
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}. Address will not be saved.", email);
                return null;
            }

            // Set the user ID
            address.UserId = user.Id;
            address.CreatedAt = DateTime.UtcNow;

            // If this is the first address or marked as default, make it the default
            var existingAddresses = await _context.SavedAddresses.Where(a => a.UserId == user.Id).ToListAsync();
            if (!existingAddresses.Any() || address.IsDefault)
            {
                // If this is the new default, unset any existing defaults
                if (address.IsDefault)
                {
                    foreach (var existingAddress in existingAddresses.Where(a => a.IsDefault))
                    {
                        existingAddress.IsDefault = false;
                        _context.Update(existingAddress);
                    }
                }

                // Make this the default if it's the first address
                if (!existingAddresses.Any())
                {
                    address.IsDefault = true;
                }
            }

            // Add the address to the database
            _context.SavedAddresses.Add(address);

            try {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved address to database with ID: {AddressId}", address.Id);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error saving address to database");
                return null;
            }

            // Verify the address was saved
            var savedAddress = await _context.SavedAddresses.FindAsync(address.Id);
            if (savedAddress == null) {
                _logger.LogWarning("Address was not found in database after saving. ID: {AddressId}", address.Id);
            } else {
                _logger.LogInformation("Verified address exists in database. ID: {AddressId}, UserId: {UserId}",
                    savedAddress.Id, savedAddress.UserId);
            }

            _logger.LogInformation("Created saved address with ID: {AddressId} for user ID: {UserId}", address.Id, user.Id);
            return address;
        }

        /// <summary>
        /// Updates an existing saved address
        /// </summary>
        public async Task<SavedAddress> UpdateSavedAddressAsync(string email, SavedAddress address)
        {
            if (string.IsNullOrEmpty(email) || address == null || address.Id <= 0)
            {
                _logger.LogWarning("Email, address, or address ID is invalid when updating saved address");
                return null;
            }

            _logger.LogInformation("Updating saved address with ID: {AddressId} for email: {Email}", address.Id, email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return null;
            }

            // Get the existing address
            var existingAddress = await _context.SavedAddresses
                .FirstOrDefaultAsync(a => a.Id == address.Id && a.UserId == user.Id);

            if (existingAddress == null)
            {
                _logger.LogWarning("Saved address not found with ID: {AddressId} for user ID: {UserId}", address.Id, user.Id);
                return null;
            }

            // Update the address properties
            existingAddress.Name = address.Name;
            existingAddress.CustomerName = address.CustomerName;
            existingAddress.CustomerPhone = address.CustomerPhone;
            existingAddress.DeliveryAddress = address.DeliveryAddress;
            existingAddress.City = address.City;
            existingAddress.State = address.State;
            existingAddress.PostalCode = address.PostalCode;
            existingAddress.UpdatedAt = DateTime.UtcNow;

            // Handle default status
            if (address.IsDefault && !existingAddress.IsDefault)
            {
                // If this is the new default, unset any existing defaults
                var otherAddresses = await _context.SavedAddresses
                    .Where(a => a.UserId == user.Id && a.Id != address.Id && a.IsDefault)
                    .ToListAsync();

                foreach (var otherAddress in otherAddresses)
                {
                    otherAddress.IsDefault = false;
                    _context.Update(otherAddress);
                }

                existingAddress.IsDefault = true;
            }

            // Update the address in the database
            _context.Update(existingAddress);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated saved address with ID: {AddressId} for user ID: {UserId}", existingAddress.Id, user.Id);
            return existingAddress;
        }

        /// <summary>
        /// Deletes a saved address
        /// </summary>
        public async Task<bool> DeleteSavedAddressAsync(string email, int addressId)
        {
            if (string.IsNullOrEmpty(email) || addressId <= 0)
            {
                _logger.LogWarning("Email or address ID is invalid when deleting saved address");
                return false;
            }

            _logger.LogInformation("Deleting saved address with ID: {AddressId} for email: {Email}", addressId, email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return false;
            }

            // Get the address
            var address = await _context.SavedAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == user.Id);

            if (address == null)
            {
                _logger.LogWarning("Saved address not found with ID: {AddressId} for user ID: {UserId}", addressId, user.Id);
                return false;
            }

            // Check if this is the default address
            bool wasDefault = address.IsDefault;

            // Remove the address
            _context.SavedAddresses.Remove(address);
            await _context.SaveChangesAsync();

            // If this was the default address, set a new default if there are any addresses left
            if (wasDefault)
            {
                var remainingAddresses = await _context.SavedAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                    .ToListAsync();

                if (remainingAddresses.Any())
                {
                    var newDefault = remainingAddresses.First();
                    newDefault.IsDefault = true;
                    _context.Update(newDefault);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Set new default address with ID: {AddressId} for user ID: {UserId}", newDefault.Id, user.Id);
                }
            }

            _logger.LogInformation("Deleted saved address with ID: {AddressId} for user ID: {UserId}", addressId, user.Id);
            return true;
        }

        /// <summary>
        /// Creates a saved address from an order
        /// </summary>
        public async Task<SavedAddress> CreateFromOrderAsync(string email, Order order, string? addressName = null)
        {
            if (string.IsNullOrEmpty(email) || order == null)
            {
                _logger.LogWarning("Email or order is null when creating saved address from order");
                return null;
            }

            _logger.LogInformation("Creating saved address from order for email: {Email}", email);

            // Create a new saved address from the order
            var address = new SavedAddress
            {
                Name = addressName ?? $"Order #{order.Id}",
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                DeliveryAddress = order.DeliveryAddress,
                City = order.City,
                State = order.State,
                PostalCode = order.PostalCode,
                IsDefault = false // Don't make it default automatically
            };

            // Create the saved address
            return await CreateSavedAddressAsync(email, address);
        }

        /// <summary>
        /// Gets a specific saved address by ID
        /// </summary>
        public async Task<SavedAddress> GetSavedAddressAsync(string email, int addressId)
        {
            if (string.IsNullOrEmpty(email) || addressId <= 0)
            {
                _logger.LogWarning("Email or address ID is invalid when getting saved address");
                return null;
            }

            _logger.LogInformation("Getting saved address with ID: {AddressId} for email: {Email}", addressId, email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return null;
            }

            // Get the saved address
            var address = await _context.SavedAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == user.Id);

            if (address == null)
            {
                _logger.LogWarning("Saved address not found with ID: {AddressId} for user ID: {UserId}", addressId, user.Id);
            }

            return address;
        }

        /// <summary>
        /// Sets a saved address as the default
        /// </summary>
        public async Task<bool> SetDefaultAddressAsync(string email, int addressId)
        {
            if (string.IsNullOrEmpty(email) || addressId <= 0)
            {
                _logger.LogWarning("Email or address ID is invalid when setting default address");
                return false;
            }

            _logger.LogInformation("Setting default address with ID: {AddressId} for email: {Email}", addressId, email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return false;
            }

            // Get all addresses for this user
            var addresses = await _context.SavedAddresses
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            // Find the address to set as default
            var addressToSetDefault = addresses.FirstOrDefault(a => a.Id == addressId);
            if (addressToSetDefault == null)
            {
                _logger.LogWarning("Saved address not found with ID: {AddressId} for user ID: {UserId}", addressId, user.Id);
                return false;
            }

            // Update all addresses to not be default
            foreach (var address in addresses)
            {
                address.IsDefault = (address.Id == addressId);
                _context.Update(address);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Set address with ID: {AddressId} as default for user ID: {UserId}", addressId, user.Id);
            return true;
        }

        /// <summary>
        /// Gets the default saved address for a user
        /// </summary>
        public async Task<SavedAddress> GetDefaultAddressAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Email is null or empty when getting default address");
                return null;
            }

            _logger.LogInformation("Getting default address for email: {Email}", email);

            // Get the user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                _logger.LogWarning("User not found with email: {Email}", email);
                return null;
            }

            // Get the default address
            var address = await _context.SavedAddresses
                .Where(a => a.UserId == user.Id && a.IsDefault)
                .FirstOrDefaultAsync();

            if (address == null)
            {
                _logger.LogInformation("No default address found for user ID: {UserId}, getting most recent address", user.Id);

                // If no default address, get the most recently created one
                address = await _context.SavedAddresses
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            return address;
        }
    }
}
