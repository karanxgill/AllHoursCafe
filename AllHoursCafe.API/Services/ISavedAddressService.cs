using System.Collections.Generic;
using System.Threading.Tasks;
using AllHoursCafe.API.Models;

namespace AllHoursCafe.API.Services
{
    public interface ISavedAddressService
    {
        /// <summary>
        /// Gets all saved addresses for a user
        /// </summary>
        Task<List<SavedAddress>> GetSavedAddressesAsync(string email);

        /// <summary>
        /// Gets a specific saved address by ID
        /// </summary>
        Task<SavedAddress> GetSavedAddressAsync(string email, int addressId);

        /// <summary>
        /// Creates a new saved address for a user
        /// </summary>
        Task<SavedAddress> CreateSavedAddressAsync(string email, SavedAddress address);

        /// <summary>
        /// Updates an existing saved address
        /// </summary>
        Task<SavedAddress> UpdateSavedAddressAsync(string email, SavedAddress address);

        /// <summary>
        /// Deletes a saved address
        /// </summary>
        Task<bool> DeleteSavedAddressAsync(string email, int addressId);

        /// <summary>
        /// Sets a saved address as the default
        /// </summary>
        Task<bool> SetDefaultAddressAsync(string email, int addressId);

        /// <summary>
        /// Gets the default saved address for a user
        /// </summary>
        Task<SavedAddress> GetDefaultAddressAsync(string email);

        /// <summary>
        /// Creates a saved address from an order
        /// </summary>
        Task<SavedAddress> CreateFromOrderAsync(string email, Order order, string? addressName = null);
    }
}
