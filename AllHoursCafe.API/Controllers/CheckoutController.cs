using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AllHoursCafe.API.Controllers;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Extensions;
using AllHoursCafe.API.Models;
using AllHoursCafe.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AllHoursCafe.API.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CheckoutController> _logger;
        private readonly UserProfileService _userProfileService;
        private readonly ISavedAddressService _savedAddressService;
        private readonly PayUService _payUService;
        private readonly IEmailService _emailService;
        private readonly AppUrlService _appUrlService;

        public CheckoutController(ApplicationDbContext context, ILogger<CheckoutController> logger,
            UserProfileService userProfileService, ISavedAddressService savedAddressService, PayUService payUService,
            IEmailService emailService, AppUrlService appUrlService)
        {
            _context = context;
            _logger = logger;
            _userProfileService = userProfileService;
            _savedAddressService = savedAddressService;
            _payUService = payUService;
            _emailService = emailService;
            _appUrlService = appUrlService;
        }

        // GET: /Checkout
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return await ProcessCheckout();
        }



        // GET: /Checkout/Redirect
        [HttpGet]
        public IActionResult Redirect()
        {
            _logger.LogInformation("Redirecting to checkout page");
            return RedirectToAction("Index");
        }

        // Common checkout processing logic
        private async Task<IActionResult> ProcessCheckout()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                // Store the return URL in TempData
                TempData["ReturnUrl"] = "/Checkout";

                // Redirect to login page
                return RedirectToAction("Login", "Auth");
            }

            // Check if user has any saved addresses
            var userEmail = User.Identity.Name;
            _logger.LogInformation("Checking saved addresses for user: {Email}", userEmail);

            try {
                // First, check if there are any addresses in the database for this user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
                List<SavedAddress> savedAddresses = new List<SavedAddress>();

                if (user != null)
                {
                    // Try to get addresses directly from the database first
                    savedAddresses = await _context.SavedAddresses
                        .Where(a => a.UserId == user.Id)
                        .OrderByDescending(a => a.IsDefault)
                        .ThenByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                        .ToListAsync();

                    _logger.LogInformation("Found {Count} addresses directly from database for user ID {UserId}",
                        savedAddresses.Count, user.Id);
                }

                // If no addresses found directly, try the service
                if (savedAddresses == null || !savedAddresses.Any())
                {
                    _logger.LogInformation("No addresses found directly, trying service for user {Email}", userEmail);
                    savedAddresses = await _savedAddressService.GetSavedAddressesAsync(userEmail);
                }

                // If still no addresses, try to find by name match
                if (savedAddresses == null || !savedAddresses.Any())
                {
                    _logger.LogWarning("User {Email} has no saved addresses, trying to find by name match", userEmail);

                    if (user != null)
                    {
                        // Get all addresses to check for potential matches
                        var allAddresses = await _context.SavedAddresses.ToListAsync();
                        _logger.LogInformation("Found {Count} total addresses in database", allAddresses.Count);

                        // Try to find addresses that might belong to this user
                        var possibleAddresses = allAddresses
                            .Where(a => a.CustomerName.Contains(user.FullName, StringComparison.OrdinalIgnoreCase) ||
                                  (a.CustomerPhone == user.PhoneNumber && !string.IsNullOrEmpty(user.PhoneNumber)))
                            .ToList();

                        if (possibleAddresses.Any())
                        {
                            _logger.LogInformation("Found {Count} possible addresses by name match", possibleAddresses.Count);
                            savedAddresses = possibleAddresses;

                            // Update the user ID for these addresses
                            foreach (var addr in possibleAddresses)
                            {
                                addr.UserId = user.Id;
                                _context.Update(addr);
                            }

                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // Check for DirectAddresses in session (from debug info)
                List<SavedAddress> directAddresses = new List<SavedAddress>();
                try
                {
                    var directAddressesJson = HttpContext.Session.GetString("DirectAddresses");
                    if (!string.IsNullOrEmpty(directAddressesJson))
                    {
                        directAddresses = JsonConvert.DeserializeObject<List<SavedAddress>>(directAddressesJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving direct addresses from session");
                }

                // If neither savedAddresses nor directAddresses have addresses, redirect
                if ((savedAddresses == null || !savedAddresses.Any()) && (directAddresses == null || !directAddresses.Any()))
                {
                    _logger.LogInformation("User {Email} has no saved or direct addresses, redirecting to Address page", userEmail);
                    TempData["AddressMessage"] = "Please add a delivery address before proceeding to checkout. (No address found in your profile or session)";
                    return RedirectToAction("Index", "Address");
                }

                _logger.LogInformation("Found {Count} saved addresses and {DirectCount} direct addresses for user {Email}", savedAddresses.Count, directAddresses.Count, userEmail);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error retrieving saved addresses for user {Email}", userEmail);
                TempData["AddressError"] = "There was an error retrieving your saved addresses. Please try again.";
                return RedirectToAction("Index", "Address");
            }
            // Get cart items from session
            try {
                var cartJson = HttpContext.Session.GetString("Cart");
                _logger.LogInformation("Cart JSON: {CartJson}", cartJson ?? "null");

                var cartItems = string.IsNullOrEmpty(cartJson)
                    ? new List<CartItem>()
                    : JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

                if (cartItems == null || !cartItems.Any())
                {
                    _logger.LogWarning("Cart is empty, redirecting to menu");
                    TempData["MenuMessage"] = "Your cart is empty. Please add items to your cart before proceeding to checkout.";
                    return RedirectToAction("Index", "Menu");
                }

                _logger.LogInformation("Found {Count} items in cart", cartItems.Count);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error retrieving cart items from session");
                TempData["MenuMessage"] = "There was an error retrieving your cart. Please try again.";
                return RedirectToAction("Index", "Menu");
            }

            // Get cart items and calculate totals
            var finalCartJson = HttpContext.Session.GetString("Cart");
            var finalCartItems = string.IsNullOrEmpty(finalCartJson)
                ? new List<CartItem>()
                : JsonConvert.DeserializeObject<List<CartItem>>(finalCartJson);

            decimal subtotal = finalCartItems.Sum(item => item.Price * item.Quantity);
            decimal tax = Math.Round(subtotal * 0.05m, 2); // 5% tax
            decimal deliveryFee = 30.1007m; // Fixed delivery fee
            decimal total = subtotal + tax + deliveryFee;

            // Create view model
            var viewModel = new CheckoutViewModel
            {
                CartItems = finalCartItems,
                SubTotal = subtotal,
                Tax = tax,
                DeliveryFee = deliveryFee,
                Total = total,
                Order = new Order
                {
                    OrderType = "Delivery",
                    DeliveryTime = DateTime.Now.AddHours(1)
                }
            };

            // If user is authenticated, pre-fill the contact information from their profile
            if (User.Identity.IsAuthenticated)
            {
                var initialUserEmail = User.Identity.Name; // Email is stored in Identity.Name
                _logger.LogInformation("Retrieving user information for checkout: {Email}", initialUserEmail);

                // Use the UserProfileService to get the user's profile information
                var userProfileInfo = await _userProfileService.GetUserProfileInfoAsync(initialUserEmail);

                // Get saved addresses for this user
                try {
                    var initialSavedAddresses = await _savedAddressService.GetSavedAddressesAsync(initialUserEmail);

                    if (initialSavedAddresses != null && initialSavedAddresses.Any())
                    {
                        _logger.LogInformation("Found {Count} saved addresses for checkout", initialSavedAddresses.Count);
                        viewModel.SavedAddresses = initialSavedAddresses;

                        // If there's a default address, pre-select it
                        var defaultAddress = initialSavedAddresses.FirstOrDefault(a => a.IsDefault);
                        if (defaultAddress != null)
                        {
                            _logger.LogInformation("Using default address ID {AddressId} for checkout", defaultAddress.Id);
                            viewModel.SelectedAddressId = defaultAddress.Id;

                            // Pre-fill the order with the default address
                            viewModel.Order.CustomerName = defaultAddress.CustomerName;
                            viewModel.Order.CustomerPhone = defaultAddress.CustomerPhone;
                            viewModel.Order.DeliveryAddress = defaultAddress.DeliveryAddress;
                            viewModel.Order.City = defaultAddress.City;
                            viewModel.Order.State = defaultAddress.State;
                            viewModel.Order.PostalCode = defaultAddress.PostalCode;
                        }
                        else if (initialSavedAddresses.Any())
                        {
                            // If no default, use the first address
                            var firstAddress = initialSavedAddresses.First();
                            _logger.LogInformation("No default address, using first address ID {AddressId} for checkout", firstAddress.Id);
                            viewModel.SelectedAddressId = firstAddress.Id;

                            // Pre-fill the order with the first address
                            viewModel.Order.CustomerName = firstAddress.CustomerName;
                            viewModel.Order.CustomerPhone = firstAddress.CustomerPhone;
                            viewModel.Order.DeliveryAddress = firstAddress.DeliveryAddress;
                            viewModel.Order.City = firstAddress.City;
                            viewModel.Order.State = firstAddress.State;
                            viewModel.Order.PostalCode = firstAddress.PostalCode;
                        }
                    }
                    else {
                        _logger.LogWarning("No saved addresses found for user {Email} during checkout", initialUserEmail);
                        TempData["AddressError"] = "No saved addresses found. Please add an address before proceeding to checkout.";
                        return RedirectToAction("Index", "Address");
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error retrieving saved addresses for checkout");
                    TempData["AddressError"] = "There was an error retrieving your saved addresses. Please try again.";
                    return RedirectToAction("Index", "Address");
                }

                if (userProfileInfo != null)
                {
                    _logger.LogInformation("Found user profile info for user ID: {UserId}", userProfileInfo.UserId);

                    // Pre-fill the order with user's information
                    viewModel.Order.CustomerName = userProfileInfo.FullName;
                    viewModel.Order.CustomerEmail = userProfileInfo.Email;

                    // Pre-fill contact information if available
                    if (!string.IsNullOrEmpty(userProfileInfo.PhoneNumber))
                    {
                        viewModel.Order.CustomerPhone = userProfileInfo.PhoneNumber;
                    }

                    if (!string.IsNullOrEmpty(userProfileInfo.Address))
                    {
                        viewModel.Order.DeliveryAddress = userProfileInfo.Address;
                    }

                    if (!string.IsNullOrEmpty(userProfileInfo.City))
                    {
                        viewModel.Order.City = userProfileInfo.City;
                    }

                    if (!string.IsNullOrEmpty(userProfileInfo.State))
                    {
                        viewModel.Order.State = userProfileInfo.State;
                    }

                    if (!string.IsNullOrEmpty(userProfileInfo.PostalCode))
                    {
                        viewModel.Order.PostalCode = userProfileInfo.PostalCode;
                    }

                    // Special instructions and delivery time are intentionally left empty/default
                    viewModel.Order.SpecialInstructions = "";
                    viewModel.Order.DeliveryTime = DateTime.Now.AddHours(1);

                    _logger.LogInformation("Pre-filled checkout form with user data - Name: {Name}, Email: {Email}, Phone: {Phone}",
                        viewModel.Order.CustomerName, viewModel.Order.CustomerEmail, viewModel.Order.CustomerPhone);
                }
                else
                {
                    _logger.LogWarning("No user profile information found for email: {Email}", userEmail);
                }
            }

            return View(viewModel);
        }

        // POST: /Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CheckoutViewModel model)
        {
            _logger.LogInformation("Checkout POST action called");

            // Set default values for required fields
            if (model.Order != null)
            {
                model.Order.PaymentMethod = model.Order.PaymentMethod ?? "Pending";
                model.Order.PaymentStatus = model.Order.PaymentStatus ?? "Pending";
                model.Order.OrderStatus = model.Order.OrderStatus ?? "Pending";
                model.Order.PaymentDetails = model.Order.PaymentDetails ?? "None";
            }

            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User not authenticated, redirecting to login");
                // Store the return URL in TempData
                TempData["ReturnUrl"] = "/Checkout";

                // Redirect to login page
                return RedirectToAction("Login", "Auth");
            }

            _logger.LogInformation("User authenticated: {Email}", User.Identity.Name);

            // Check model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Validation error: {ErrorMessage}", error.ErrorMessage);
                }

                // Get cart items from session to redisplay the form
                var cartJson = HttpContext.Session.GetString("Cart");
                var cartItems = string.IsNullOrEmpty(cartJson)
                    ? new List<CartItem>()
                    : JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

                // Recalculate totals
                decimal subtotal = cartItems.Sum(item => item.Price * item.Quantity);
                decimal tax = Math.Round(subtotal * 0.05m, 2);
                decimal deliveryFee = model.Order?.OrderType == "Delivery" ? 30.00m : 0;
                decimal total = subtotal + tax + deliveryFee;

                // Update model with cart items and totals
                model.CartItems = cartItems;
                model.SubTotal = subtotal;
                model.Tax = tax;
                model.DeliveryFee = deliveryFee;
                model.Total = total;

                return View(model);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation("Processing checkout with valid model");

                    // Get cart items from session
                    var cartJson = HttpContext.Session.GetString("Cart");
                    _logger.LogInformation("Cart JSON: {CartJson}", cartJson ?? "null");

                    var cartItems = string.IsNullOrEmpty(cartJson)
                        ? new List<CartItem>()
                        : JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

                    if (cartItems == null || !cartItems.Any())
                    {
                        _logger.LogWarning("Cart is empty, redirecting to menu");
                        return RedirectToAction("Index", "Menu");
                    }

                    // Check if an address is selected
                    if (model.SelectedAddressId == null)
                    {
                        _logger.LogWarning("No address selected, redirecting to address page");
                        TempData["AddressMessage"] = "Please select a delivery address before proceeding to checkout.";
                        return RedirectToAction("Index", "Address");
                    }

                    _logger.LogInformation("Selected address ID: {AddressId}", model.SelectedAddressId);

                    // Get the selected address
                    string checkoutUserEmail = User.Identity.Name;
                    SavedAddress selectedAddress = null;

                    try {
                        var checkoutAddresses = await _savedAddressService.GetSavedAddressesAsync(checkoutUserEmail);

                        if (checkoutAddresses == null || !checkoutAddresses.Any())
                        {
                            _logger.LogWarning("No addresses found for user {Email}, redirecting to address page", checkoutUserEmail);
                            TempData["AddressError"] = "No saved addresses found. Please add an address before proceeding to checkout.";
                            return RedirectToAction("Index", "Address");
                        }

                        _logger.LogInformation("Found {Count} addresses for user {Email}", checkoutAddresses.Count, checkoutUserEmail);

                        selectedAddress = checkoutAddresses.FirstOrDefault(a => a.Id == model.SelectedAddressId);

                        if (selectedAddress == null)
                        {
                            _logger.LogWarning("Selected address ID {AddressId} not found, trying to use default address", model.SelectedAddressId);

                            // Try to use default address instead
                            selectedAddress = checkoutAddresses.FirstOrDefault(a => a.IsDefault);

                            if (selectedAddress == null)
                            {
                                _logger.LogWarning("No default address found, trying to use first address");
                                selectedAddress = checkoutAddresses.FirstOrDefault();

                                if (selectedAddress == null)
                                {
                                    _logger.LogWarning("No addresses available, redirecting to address page");
                                    TempData["AddressError"] = "The selected address was not found. Please select a valid address.";
                                    return RedirectToAction("Index", "Address");
                                }
                            }

                            _logger.LogInformation("Using alternative address ID: {AddressId}", selectedAddress.Id);
                            model.SelectedAddressId = selectedAddress.Id;
                        }
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Error retrieving addresses for checkout");
                        TempData["AddressError"] = "There was an error retrieving your saved addresses. Please try again.";
                        return RedirectToAction("Index", "Address");
                    }

                    // Update the order with the selected address
                    model.Order.CustomerName = selectedAddress.CustomerName;
                    model.Order.CustomerPhone = selectedAddress.CustomerPhone;
                    model.Order.DeliveryAddress = selectedAddress.DeliveryAddress;
                    model.Order.City = selectedAddress.City;
                    model.Order.State = selectedAddress.State;
                    model.Order.PostalCode = selectedAddress.PostalCode;

                    _logger.LogInformation("Cart has {Count} items", cartItems.Count);

                    // Calculate totals
                    decimal subtotal = cartItems.Sum(item => item.Price * item.Quantity);
                    decimal tax = Math.Round(subtotal * 0.05m, 2); // 5% tax
                    decimal deliveryFee = model.Order.OrderType == "Delivery" ? 30.00m : 0; // Fee only for delivery
                    decimal total = subtotal + tax + deliveryFee;

                    // Create order
                    var order = new Order
                    {
                        CustomerName = model.Order.CustomerName,
                        CustomerEmail = model.Order.CustomerEmail,
                        CustomerPhone = model.Order.CustomerPhone,
                        DeliveryAddress = model.Order.DeliveryAddress,
                        City = model.Order.City,
                        State = model.Order.State,
                        PostalCode = model.Order.PostalCode,
                        SpecialInstructions = model.Order.SpecialInstructions,
                        OrderType = model.Order.OrderType,
                        DeliveryTime = model.Order.DeliveryTime,
                        SubTotal = subtotal,
                        Tax = tax,
                        DeliveryFee = deliveryFee,
                        Total = total,
                        PaymentMethod = "Pending",
                        PaymentStatus = "Pending",
                        OrderStatus = "Pending",
                        PaymentDetails = "None",
                        OrderDate = DateTime.Now,
                        OrderItems = cartItems.Select(item => new OrderItem
                        {
                            MenuItemId = item.Id,
                            Name = item.Name,
                            Price = item.Price,
                            Quantity = item.Quantity,
                            Total = item.Price * item.Quantity
                        }).ToList()
                    };

                    // Update user information with checkout details if user is logged in
                    if (User.Identity.IsAuthenticated)
                    {
                        var profileUserEmail = User.Identity.Name; // Email is stored in Identity.Name
                        _logger.LogInformation("Updating user information for: {Email}", profileUserEmail);

                        // Use the UserProfileService to update the user's profile
                        await _userProfileService.UpdateUserProfileAsync(profileUserEmail, order);
                        _logger.LogInformation("User profile updated with order information");

                        // Address saving functionality has been removed
                    }
                    else
                    {
                        _logger.LogWarning("User not authenticated when updating profile");
                    }

                    // Save order to database
                    _logger.LogInformation("Saving order to database");
                    _context.Orders.Add(order);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Order saved with ID: {OrderId}", order.Id);

                    // Store order ID in session for payment page
                    HttpContext.Session.SetInt32("CurrentOrderId", order.Id);
                    _logger.LogInformation("Stored order ID in session: {OrderId}", order.Id);

                    // Redirect to payment page
                    _logger.LogInformation("Redirecting to payment page for order: {OrderId}", order.Id);
                    TempData["OrderId"] = order.Id;
                    TempData["RedirectToPayment"] = Url.Action("Payment", "Checkout", new { id = order.Id });

                    // Redirect to the PayU payment page
                    return RedirectToAction("SimplePayment", new { id = order.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing checkout: {Message}", ex.Message);
                    if (ex.InnerException != null)
                    {
                        _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                    }
                    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                    ModelState.AddModelError("", "An error occurred while processing your order. Please try again.");
                }
            }

            // If we got this far, something failed, redisplay form
            // Recalculate totals
            var recalcCartJson = HttpContext.Session.GetString("Cart");
            var recalcCartItems = string.IsNullOrEmpty(recalcCartJson)
                ? new List<CartItem>()
                : JsonConvert.DeserializeObject<List<CartItem>>(recalcCartJson);

            decimal recalcSubtotal = recalcCartItems.Sum(item => item.Price * item.Quantity);
            decimal recalcTax = Math.Round(recalcSubtotal * 0.05m, 2);
            decimal recalcDeliveryFee = model.Order.OrderType == "Delivery" ? 30.00m : 0;
            decimal recalcTotal = recalcSubtotal + recalcTax + recalcDeliveryFee;

            model.CartItems = recalcCartItems;
            model.SubTotal = recalcSubtotal;
            model.Tax = recalcTax;
            model.DeliveryFee = recalcDeliveryFee;
            model.Total = recalcTotal;

            return View(model);
        }

        // GET: /Checkout/Payment/5
        public async Task<IActionResult> Payment(int id)
        {
            _logger.LogInformation("Payment GET action called for order ID: {OrderId}", id);

            // Check if we have an order ID from TempData (for direct redirects)
            if (TempData["OrderId"] != null)
            {
                int tempOrderId = (int)TempData["OrderId"];
                _logger.LogInformation("Found order ID in TempData: {OrderId}", tempOrderId);

                // If the URL doesn't match the TempData order ID, redirect to the correct URL
                if (id != tempOrderId)
                {
                    _logger.LogInformation("Redirecting to correct payment URL for order ID: {OrderId}", tempOrderId);
                    return RedirectToAction("Payment", new { id = tempOrderId });
                }
            }

            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                _logger.LogWarning("Order not found with ID: {OrderId}", id);
                return NotFound();
            }

            _logger.LogInformation("Found order with ID: {OrderId}, Customer: {Customer}, Total: {Total}",
                order.Id, order.CustomerName, order.Total);

            // Verify this order belongs to the current session
            var sessionOrderId = HttpContext.Session.GetInt32("CurrentOrderId");
            _logger.LogInformation("Session order ID: {SessionOrderId}, Requested order ID: {OrderId}", sessionOrderId, id);

            // If session order ID is null but we have a valid order, set it in the session
            if (sessionOrderId == null)
            {
                _logger.LogInformation("Setting session order ID to: {OrderId}", id);
                HttpContext.Session.SetInt32("CurrentOrderId", id);
            }
            else if (sessionOrderId != id)
            {
                _logger.LogWarning("Order ID mismatch. Session: {SessionOrderId}, Requested: {OrderId}", sessionOrderId, id);
                // Instead of redirecting, update the session to match this order
                HttpContext.Session.SetInt32("CurrentOrderId", id);
                _logger.LogInformation("Updated session order ID to: {OrderId}", id);
            }

            var viewModel = new PaymentViewModel
            {
                OrderId = order.Id,
                Total = order.Total,
                PaymentMethods = new List<string> { "PayU" }
            };

            _logger.LogInformation("Returning payment view for order ID: {OrderId}", id);
            return View(viewModel);
        }

        // POST: /Checkout/Payment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(int id, PaymentViewModel model)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            // Verify this order belongs to the current session
            var sessionOrderId = HttpContext.Session.GetInt32("CurrentOrderId");
            if (sessionOrderId != id)
            {
                return RedirectToAction("Index", "Home");
            }

            _logger.LogInformation("ModelState.IsValid: {IsValid}", ModelState.IsValid);
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("ModelState error: {ErrorMessage}", error.ErrorMessage);
                }
            }
            if (ModelState.IsValid)
            {
                try
                {
                    // Update order with payment information
                    order.PaymentMethod = model.PaymentMethod;

                    // Only PayU is supported now
                    if (model.PaymentMethod == "PayU")
                    {
                        _logger.LogInformation("PayU payment branch hit for order ID: {OrderId}", order.Id);
                        var txnid = Guid.NewGuid().ToString();
                        var payUParams = new Dictionary<string, string>
                        {
                            { "key", _payUService.MerchantKey },
                            { "txnid", txnid },
                            { "amount", order.Total.ToString("F2") },
                            { "productinfo", $"Order_{order.Id}" },
                            { "firstname", order.CustomerName ?? string.Empty },
                            { "email", order.CustomerEmail ?? string.Empty },
                            { "phone", order.CustomerPhone ?? string.Empty },
                            { "surl", _appUrlService.GetUrl($"Checkout/PaymentSuccess/{order.Id}") },
                            { "furl", _appUrlService.GetUrl($"Checkout/PaymentFailure/{order.Id}") },
                            // Add any other required fields if needed
                        };

                        string hash = _payUService.GenerateHash(payUParams);
                        string payuForm = _payUService.GetPayUForm(payUParams, hash);
                        ViewBag.PayUForm = payuForm;
                        return View("PayUForm");
                    }

                    // If other payment methods are ever added, handle them here
                    // For now, fallback (should not be hit)
                    order.PaymentDetails = model.PaymentMethod;

                    _context.Update(order);
                    await _context.SaveChangesAsync();

                    // If user is authenticated, update their information
                    if (User.Identity.IsAuthenticated)
                    {
                        var userEmail = User.Identity.Name;
                        _logger.LogInformation("Updating user information during payment for: {Email}", userEmail);

                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

                        if (user != null)
                        {
                            _logger.LogInformation("Found user with ID: {UserId} during payment processing", user.Id);

                            // Always update user information with the latest order details
                            user.PhoneNumber = order.CustomerPhone;
                            user.Address = order.DeliveryAddress;
                            user.City = order.City;
                            user.State = order.State;
                            user.PostalCode = order.PostalCode;

                            _logger.LogInformation("Updated user fields during payment - Phone: {Phone}, Address: {Address}, City: {City}, State: {State}, PostalCode: {PostalCode}",
                                user.PhoneNumber, user.Address, user.City, user.State, user.PostalCode);

                            _context.Update(user);
                            await _context.SaveChangesAsync();

                            // Reload the user from the database to verify the change
                            await _context.Entry(user).ReloadAsync();

                            _logger.LogInformation("User information updated during payment processing - User ID: {UserId}, Phone: {Phone}, Address: {Address}",
                                user.Id, user.PhoneNumber, user.Address);
                        }
                        else
                        {
                            _logger.LogWarning("User not found with email during payment: {Email}", userEmail);
                        }
                    }

                    // Clear the cart
                    HttpContext.Session.Remove("Cart");

                    // Add success message to TempData
                    TempData["SuccessMessage"] = "Payment completed successfully! Your order has been confirmed.";

                    // Redirect to the simple success page
                    return RedirectToAction("PaymentSuccess", new { id = order.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing payment");
                    ModelState.AddModelError("", "An error occurred while processing your payment. Please try again.");
                }
            }

            // If we got this far, something failed, redisplay form
            model.OrderId = order.Id;
            model.Total = order.Total;
            model.PaymentMethods = new List<string> { "PayU" };

            return View(model);
        }

        // GET: /Checkout/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            // Verify this order belongs to the current session
            var sessionOrderId = HttpContext.Session.GetInt32("CurrentOrderId");
            if (sessionOrderId != id)
            {
                return RedirectToAction("Index", "Home");
            }

            // Clear the current order from session
            HttpContext.Session.Remove("CurrentOrderId");

            return View(order);
        }

        // GET: /Checkout/OrderSuccess/5
        public async Task<IActionResult> OrderSuccess(int id)
        {
            _logger.LogInformation("OrderSuccess action called for order ID: {OrderId}", id);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found with ID: {OrderId}", id);
                return NotFound();
            }

            _logger.LogInformation("Found order with ID: {OrderId}, Customer: {Customer}, Total: {Total}",
                order.Id, order.CustomerName, order.Total);

            // Clear the current order from session
            HttpContext.Session.Remove("CurrentOrderId");

            // Update user information if authenticated
            if (User.Identity.IsAuthenticated)
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Updating user information for: {Email} in OrderSuccess", userEmail);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

                if (user != null)
                {
                    _logger.LogInformation("Found user with ID: {UserId} in OrderSuccess", user.Id);

                    // Update user's contact and address information
                    user.PhoneNumber = order.CustomerPhone;
                    user.Address = order.DeliveryAddress;
                    user.City = order.City;
                    user.State = order.State;
                    user.PostalCode = order.PostalCode;

                    _logger.LogInformation("Updated user fields in OrderSuccess - Phone: {Phone}, Address: {Address}",
                        user.PhoneNumber, user.Address);

                    // Save the changes to the database
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully saved user profile in OrderSuccess - User ID: {UserId}", user.Id);
                }
            }

            return View(order);
        }

        // GET: /Checkout/PaymentSuccess/5
        public async Task<IActionResult> PaymentSuccess(int id)
        {
            _logger.LogInformation("PaymentSuccess action called for order ID: {OrderId}", id);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found with ID: {OrderId}", id);
                return NotFound();
            }

            _logger.LogInformation("Found order with ID: {OrderId}, Customer: {Customer}, Total: {Total}",
                order.Id, order.CustomerName, order.Total);

            // Update order status
            order.PaymentStatus = "Completed";
            order.OrderStatus = "Confirmed";
            order.PaymentMethod = "PayU";
            order.PaymentDetails = "Payment completed via PayU";

            _context.Update(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated order status to Completed for order ID: {OrderId}", id);

            // Send order confirmation email
            await SendOrderConfirmationEmailAsync(order);

            // Clear the current order from session
            HttpContext.Session.Remove("CurrentOrderId");

            // Update user information if authenticated
            if (User.Identity.IsAuthenticated)
            {
                var userEmail = User.Identity.Name;
                _logger.LogInformation("Updating user information for: {Email} in PaymentSuccess", userEmail);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

                if (user != null)
                {
                    _logger.LogInformation("Found user with ID: {UserId} in PaymentSuccess", user.Id);

                    // Update user's contact and address information
                    user.PhoneNumber = order.CustomerPhone;
                    user.Address = order.DeliveryAddress;
                    user.City = order.City;
                    user.State = order.State;
                    user.PostalCode = order.PostalCode;

                    _logger.LogInformation("Updated user fields in PaymentSuccess - Phone: {Phone}, Address: {Address}",
                        user.PhoneNumber, user.Address);

                    // Save the changes to the database
                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully saved user profile in PaymentSuccess - User ID: {UserId}", user.Id);
                }
            }

            return View(order);
        }

        // GET: /Checkout/PaymentFailure/5
        public async Task<IActionResult> PaymentFailure(int id)
        {
            _logger.LogInformation("PaymentFailure action called for order ID: {OrderId}", id);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                _logger.LogWarning("Order not found with ID: {OrderId}", id);
                return NotFound();
            }

            _logger.LogInformation("Found order with ID: {OrderId}, Customer: {Customer}, Total: {Total}",
                order.Id, order.CustomerName, order.Total);

            // Update order status
            order.PaymentStatus = "Failed";
            order.PaymentDetails = "Payment failed via PayU";

            _context.Update(order);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated order status to Failed for order ID: {OrderId}", id);

            // Add error message to TempData
            TempData["ErrorMessage"] = "Your payment could not be processed. Please try again or contact customer support.";

            return View(order);
        }

        // GET: /Checkout/DirectPayment/5
        public IActionResult DirectPayment(int id)
        {
            _logger.LogInformation("DirectPayment action called for order ID: {OrderId}", id);

            // Store the order ID in session and ViewBag
            HttpContext.Session.SetInt32("CurrentOrderId", id);
            ViewBag.OrderId = id;

            return View();
        }

        // GET: /Checkout/SimplePayment/5
        public async Task<IActionResult> SimplePayment(int id)
        {
            _logger.LogInformation("SimplePayment action called for order ID: {OrderId}", id);

            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            // Store the order ID in session
            HttpContext.Session.SetInt32("CurrentOrderId", id);

            var viewModel = new PaymentViewModel
            {
                OrderId = order.Id,
                Total = order.Total,
                PaymentMethods = new List<string> { "PayU" }
            };

            return View(viewModel);
        }

        // Private method to send order confirmation email
        private async Task SendOrderConfirmationEmailAsync(Order order)
        {
            try
            {
                _logger.LogInformation("Sending order confirmation email to {Email} for order {OrderId}",
                    order.CustomerEmail, order.Id);

                // Format order items for email
                var orderItemsHtml = "";
                foreach (var item in order.OrderItems)
                {
                    orderItemsHtml += $"<tr>\n" +
                        $"  <td style=\"padding: 10px; border-bottom: 1px solid #ddd;\">{item.Name}</td>\n" +
                        $"  <td style=\"padding: 10px; border-bottom: 1px solid #ddd; text-align: center;\">{item.Quantity}</td>\n" +
                        $"  <td style=\"padding: 10px; border-bottom: 1px solid #ddd; text-align: right;\">₹{item.Price.ToString("F2")}</td>\n" +
                        $"  <td style=\"padding: 10px; border-bottom: 1px solid #ddd; text-align: right;\">₹{item.Total.ToString("F2")}</td>\n" +
                        $"</tr>";
                }

                // Create email body with proper escaping
                string emailBody = $"<html>\n" +
                    $"<head>\n" +
                    $"  <style>\n" +
                    $"    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}\n" +
                    $"    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}\n" +
                    $"    .header {{ background-color: #5c4033; color: white; padding: 20px; text-align: center; }}\n" +
                    $"    .content {{ padding: 20px; background-color: #f9f9f9; }}\n" +
                    $"    .order-details {{ margin-bottom: 20px; }}\n" +
                    $"    .order-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}\n" +
                    $"    .order-table th {{ background-color: #f0f0f0; padding: 10px; text-align: left; border-bottom: 2px solid #ddd; }}\n" +
                    $"    .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #777; }}\n" +
                    $"    .total-row {{ font-weight: bold; background-color: #f5f5f5; }}\n" +
                    $"  </style>\n" +
                    $"</head>\n" +
                    $"<body>\n" +
                    $"  <div class=\"container\">\n" +
                    $"    <div class=\"header\">\n" +
                    $"      <h1>Order Confirmed!</h1>\n" +
                    $"      <p>Thank you for your order</p>\n" +
                    $"    </div>\n" +
                    $"    <div class=\"content\">\n" +
                    $"      <p>Dear {order.CustomerName},</p>\n" +
                    $"      <p>Your order has been received and confirmed. We're preparing your delicious food right now!</p>\n" +
                    $"      \n" +
                    $"      <div class=\"order-details\">\n" +
                    $"        <h2>Order Details</h2>\n" +
                    $"        <p><strong>Order Number:</strong> #{order.Id}</p>\n" +
                    $"        <p><strong>Order Date:</strong> {order.OrderDate.ToString("MMMM dd, yyyy h:mm tt")}</p>\n" +
                    $"        <p><strong>Payment Method:</strong> {order.PaymentMethod}</p>\n" +
                    $"        <p><strong>Order Type:</strong> {order.OrderType}</p>\n" +
                    $"      </div>\n" +
                    $"      \n" +
                    $"      <div class=\"delivery-details\">\n" +
                    $"        <h2>Delivery Information</h2>\n" +
                    $"        <p><strong>Name:</strong> {order.CustomerName}</p>\n" +
                    $"        <p><strong>Email:</strong> {order.CustomerEmail}</p>\n" +
                    $"        <p><strong>Phone:</strong> {order.CustomerPhone}</p>\n" +
                    $"        <p><strong>Address:</strong> {order.DeliveryAddress}, {order.City}, {order.State} {order.PostalCode}</p>\n" +
                    $"      </div>\n" +
                    $"      \n" +
                    $"      <h2>Order Summary</h2>\n" +
                    $"      <table class=\"order-table\">\n" +
                    $"        <thead>\n" +
                    $"          <tr>\n" +
                    $"            <th style=\"padding: 10px; text-align: left;\">Item</th>\n" +
                    $"            <th style=\"padding: 10px; text-align: center;\">Quantity</th>\n" +
                    $"            <th style=\"padding: 10px; text-align: right;\">Price</th>\n" +
                    $"            <th style=\"padding: 10px; text-align: right;\">Total</th>\n" +
                    $"          </tr>\n" +
                    $"        </thead>\n" +
                    $"        <tbody>\n" +
                    $"          {orderItemsHtml}\n" +
                    $"        </tbody>\n" +
                    $"        <tfoot>\n" +
                    $"          <tr>\n" +
                    $"            <td colspan=\"3\" style=\"padding: 10px; text-align: right; font-weight: bold;\">Subtotal:</td>\n" +
                    $"            <td style=\"padding: 10px; text-align: right; font-weight: bold;\">₹{order.SubTotal.ToString("F2")}</td>\n" +
                    $"          </tr>\n" +
                    $"          <tr>\n" +
                    $"            <td colspan=\"3\" style=\"padding: 10px; text-align: right; font-weight: bold;\">Tax (5%):</td>\n" +
                    $"            <td style=\"padding: 10px; text-align: right; font-weight: bold;\">₹{order.Tax.ToString("F2")}</td>\n" +
                    $"          </tr>\n" +
                    $"{(order.DeliveryFee > 0 ? $"          <tr>\n" +
                    $"            <td colspan=\"3\" style=\"padding: 10px; text-align: right; font-weight: bold;\">Delivery Fee:</td>\n" +
                    $"            <td style=\"padding: 10px; text-align: right; font-weight: bold;\">₹{order.DeliveryFee.ToString("F2")}</td>\n" +
                    $"          </tr>\n" : "")}" +
                    $"          <tr class=\"total-row\">\n" +
                    $"            <td colspan=\"3\" style=\"padding: 10px; text-align: right; font-weight: bold; background-color: #f5f5f5;\">Total:</td>\n" +
                    $"            <td style=\"padding: 10px; text-align: right; font-weight: bold; background-color: #f5f5f5;\">₹{order.Total.ToString("F2")}</td>\n" +
                    $"          </tr>\n" +
                    $"        </tfoot>\n" +
                    $"      </table>\n" +
                    $"      \n" +
                    $"      <p>If you have any questions about your order, please contact us at support@allhourscafe.com or call us at +91 123-456-7890.</p>\n" +
                    $"      <p>Thank you for choosing All Hours Cafe!</p>\n" +
                    $"    </div>\n" +
                    $"    <div class=\"footer\">\n" +
                    $"      <p>© {DateTime.Now.Year} All Hours Cafe. All rights reserved.</p>\n" +
                    $"      <p>This is an automated email, please do not reply.</p>\n" +
                    $"    </div>\n" +
                    $"  </div>\n" +
                    $"</body>\n" +
                    $"</html>";

                // Send the email
                await _emailService.SendEmailAsync(order.CustomerEmail, $"All Hours Cafe - Order Confirmation #{order.Id}", emailBody);
                _logger.LogInformation("Order confirmation email sent successfully to {Email} for order {OrderId}",
                    order.CustomerEmail, order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order confirmation email to {Email} for order {OrderId}",
                    order.CustomerEmail, order.Id);
                // Don't throw the exception - we don't want to disrupt the order process if email fails
            }
        }

        // GET: /Checkout/Debug
        public async Task<IActionResult> Debug()
        {
            _logger.LogInformation("Debug action called");

            // Check if user is authenticated
            bool isAuthenticated = User.Identity.IsAuthenticated;
            string userEmail = isAuthenticated ? User.Identity.Name : "Not authenticated";

            // Get cart items from session
            var cartJson = HttpContext.Session.GetString("Cart");
            var cartItems = string.IsNullOrEmpty(cartJson)
                ? new List<CartItem>()
                : JsonConvert.DeserializeObject<List<CartItem>>(cartJson);

            // Get current order ID from session
            var currentOrderId = HttpContext.Session.GetInt32("CurrentOrderId");

            // Get saved addresses if authenticated
            List<SavedAddress> savedAddresses = null;
            List<SavedAddress> directAddresses = null;
            User currentUser = null;
            List<User> allUsers = null;
            List<SavedAddress> allAddresses = null;

            if (isAuthenticated)
            {
                try
                {
                    // Get addresses through service
                    savedAddresses = await _savedAddressService.GetSavedAddressesAsync(userEmail);

                    // Also try to get user and addresses directly for debugging
                    currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
                    allUsers = await _context.Users.ToListAsync();
                    allAddresses = await _context.SavedAddresses.ToListAsync();

                    if (currentUser != null)
                    {
                        directAddresses = await _context.SavedAddresses
                            .Where(a => a.UserId == currentUser.Id)
                            .OrderByDescending(a => a.IsDefault)
                            .ToListAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving saved addresses for debug");
                }
            }

            // Create debug info
            var debugInfo = new
            {
                IsAuthenticated = isAuthenticated,
                UserEmail = userEmail,
                CartItems = cartItems,
                CartItemCount = cartItems?.Count ?? 0,
                CurrentOrderId = currentOrderId,
                SessionKeys = HttpContext.Session.Keys.ToList(),
                SavedAddresses = savedAddresses,
                SavedAddressCount = savedAddresses?.Count ?? 0,
                CurrentUser = currentUser != null ? new { currentUser.Id, currentUser.Email, currentUser.FullName } : null,
                DirectAddresses = directAddresses,
                DirectAddressCount = directAddresses?.Count ?? 0,
                AllUsersCount = allUsers?.Count ?? 0,
                AllUsers = allUsers?.Select(u => new { u.Id, u.Email }).ToList(),
                AllAddressesCount = allAddresses?.Count ?? 0,
                AllAddresses = allAddresses?.Select(a => new { a.Id, a.UserId, a.Name, a.CustomerName }).ToList()
            };

            return Json(debugInfo);
        }
    }

    public class CheckoutViewModel
    {
        public List<CartItem> CartItems { get; set; } = new List<CartItem>();
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal Total { get; set; }
        public Order Order { get; set; } = new Order();
        public List<SavedAddress> SavedAddresses { get; set; } = new List<SavedAddress>();
        public int? SelectedAddressId { get; set; }
    }

    public class PaymentViewModel
    {
        public int OrderId { get; set; }
        public decimal Total { get; set; }
        public List<string> PaymentMethods { get; set; } = new List<string>();
        public string PaymentMethod { get; set; } = "Credit Card";
        public string? CardNumber { get; set; }
        public string? UPIId { get; set; }
    }
}