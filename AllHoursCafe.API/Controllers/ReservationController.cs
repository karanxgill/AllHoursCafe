using System;
using System.Threading.Tasks;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AllHoursCafe.API.Services;

namespace AllHoursCafe.API.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReservationController> _logger;
        private readonly PayUService _payUService;

        public ReservationController(ApplicationDbContext context, ILogger<ReservationController> logger, PayUService payUService)
        {
            _context = context;
            _logger = logger;
            _payUService = payUService;
        }

        // GET: /Reservation
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Reservation/Create
        public IActionResult Create()
        {
            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                // Store the return URL in TempData
                TempData["ReturnUrl"] = "/Reservation/Create";

                // Redirect to login page
                return RedirectToAction("Login", "Auth");
            }

            // Set default reservation date and time
            var reservation = new Reservation
            {
                ReservationDate = DateTime.Today.AddDays(1),
                ReservationTime = DateTime.Today.AddHours(18) // Default to 6 PM
            };

            return View(reservation);
        }

        // POST: /Reservation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Reservation reservation)
        {
            _logger.LogInformation("Create POST action called with reservation data: Name={Name}, Email={Email}, Date={Date}",
                reservation.Name, reservation.Email, reservation.ReservationDate);

            // Check if user is authenticated
            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("User not authenticated when attempting to create reservation");
                // Store the return URL in TempData
                TempData["ReturnUrl"] = "/Reservation/Create";

                // Redirect to login page
                return RedirectToAction("Login", "Auth");
            }

            // Remove any validation errors for PaymentTxnId since it's optional
            if (ModelState.ContainsKey("PaymentTxnId"))
            {
                ModelState.Remove("PaymentTxnId");
            }

            // Log any remaining validation errors
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Model state invalid for reservation creation");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning("Validation error: {ErrorMessage}", error.ErrorMessage);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Combine date and time
                    var reservationDateTime = reservation.ReservationDate.Date.Add(reservation.ReservationTime.TimeOfDay);
                    _logger.LogInformation("Combined reservation date and time: {DateTime}", reservationDateTime);

                    // Ensure reservation is in the future
                    if (reservationDateTime <= DateTime.Now)
                    {
                        _logger.LogWarning("Reservation time is not in the future: {DateTime}", reservationDateTime);
                        ModelState.AddModelError("ReservationDate", "Reservation must be for a future date and time.");
                        return View(reservation);
                    }

                    // Set creation time and payment defaults
                    reservation.CreatedAt = DateTime.Now;
                    reservation.PaymentStatus = "Pending";
                    reservation.PaymentMethod = "PayU";
                    reservation.PaymentAmount = 500.00m;

                    // Add to database
                    _context.Reservations.Add(reservation);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("New reservation created with ID: {ReservationId} for {Name} on {DateTime}",
                        reservation.Id, reservation.Name, reservationDateTime);

                    // Redirect to payment page
                    _logger.LogInformation("Redirecting to Payment action with ID: {ReservationId}", reservation.Id);
                    return RedirectToAction(nameof(Payment), new { id = reservation.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating reservation");
                    ModelState.AddModelError("", "An error occurred while processing your reservation. Please try again.");
                }
            }

            _logger.LogWarning("Returning to Create view due to validation errors or exception");
            return View(reservation);
        }

        // GET: /Reservation/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);

            if (reservation == null)
            {
                return NotFound();
            }

            return View(reservation);
        }
        // GET: /Reservation/Payment/{id}
        public async Task<IActionResult> Payment(int id)
        {
            _logger.LogInformation("Payment action called for reservation ID: {ReservationId}", id);

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning("Reservation not found with ID: {ReservationId}", id);
                return NotFound();
            }

            _logger.LogInformation("Found reservation: Name={Name}, Email={Email}, Date={Date}",
                reservation.Name, reservation.Email, reservation.ReservationDate);

            var viewModel = new ReservationPaymentViewModel
            {
                ReservationId = reservation.Id,
                Name = reservation.Name,
                Email = reservation.Email,
                PhoneNumber = reservation.PhoneNumber,
                Amount = 500m,
                PaymentMethod = "PayU"
            };

            try
            {
                // Always generate a new transaction ID for better tracking
                var txnid = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated transaction ID: {TransactionId}", txnid);

                var payUParams = new Dictionary<string, string>
                {
                    { "key", _payUService.MerchantKey },
                    { "txnid", txnid },
                    { "amount", viewModel.Amount.ToString("F2") },
                    { "productinfo", $"Reservation_{reservation.Id}" },
                    { "firstname", reservation.Name ?? string.Empty },
                    { "email", reservation.Email ?? string.Empty },
                    { "phone", reservation.PhoneNumber ?? string.Empty },
                    { "surl", "http://localhost:5002/Reservation/PaymentSuccess/" + reservation.Id },
                    { "furl", "http://localhost:5002/Reservation/PaymentFailure/" + reservation.Id }
                };

                // Save transaction ID and ensure payment method is set
                reservation.PaymentTxnId = txnid;
                reservation.PaymentMethod = "PayU";
                reservation.PaymentStatus = "Pending";
                _context.Update(reservation);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated reservation with transaction ID: {TransactionId}", txnid);

                // Redirect to the processing page
                _logger.LogInformation("Redirecting to ProcessPayU action with ID: {ReservationId}", reservation.Id);
                return RedirectToAction(nameof(ProcessPayU), new { id = reservation.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Payment action for reservation ID: {ReservationId}", id);
                TempData["ErrorMessage"] = "An error occurred while processing your payment. Please try again.";
                return RedirectToAction(nameof(Create));
            }
        }

        // GET: /Reservation/PaymentSuccess/{id}
        public async Task<IActionResult> PaymentSuccess(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            reservation.PaymentStatus = "Completed";
            reservation.PaymentMethod = "PayU";
            reservation.IsConfirmed = true;
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your reservation is confirmed and payment completed!";
            return RedirectToAction("Confirmation", new { id });
        }

        // GET: /Reservation/PaymentFailure/{id}
        public async Task<IActionResult> PaymentFailure(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            reservation.PaymentStatus = "Failed";
            reservation.PaymentMethod = "PayU";
            reservation.IsConfirmed = false;
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Payment failed. Your reservation is not confirmed. Please try again.";
            return RedirectToAction("Confirmation", new { id });
        }

        // GET: /Reservation/ProcessPayU/{id}
        public async Task<IActionResult> ProcessPayU(int id)
        {
            _logger.LogInformation("ProcessPayU action called for reservation ID: {ReservationId}", id);

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                _logger.LogWarning("Reservation not found with ID: {ReservationId}", id);
                return NotFound();
            }

            try
            {
                _logger.LogInformation("Preparing PayU form for reservation ID: {ReservationId}", id);

                // Always generate a new transaction ID for better tracking
                var txnid = Guid.NewGuid().ToString();
                _logger.LogInformation("Generated new transaction ID: {TransactionId}", txnid);

                // Save the new transaction ID
                reservation.PaymentTxnId = txnid;
                _context.Update(reservation);
                await _context.SaveChangesAsync();

                // Prepare PayU form with custom styling and no auto-submit
                var payUParams = new Dictionary<string, string>
                {
                    { "key", _payUService.MerchantKey },
                    { "txnid", txnid },
                    { "amount", "500.00" },
                    { "productinfo", $"Reservation_{reservation.Id}" },
                    { "firstname", reservation.Name ?? string.Empty },
                    { "email", reservation.Email ?? string.Empty },
                    { "phone", reservation.PhoneNumber ?? string.Empty },
                    { "surl", "http://localhost:5002/Reservation/PaymentSuccess/" + reservation.Id },
                    { "furl", "http://localhost:5002/Reservation/PaymentFailure/" + reservation.Id }
                };

                string hash = _payUService.GenerateHash(payUParams);
                _logger.LogInformation("Generated hash for PayU form");

                string payuForm = _payUService.GetPayUForm(payUParams, hash, false, "btn btn-primary btn-lg");
                ViewBag.PayUForm = payuForm;

                _logger.LogInformation("Successfully prepared PayU form, returning view");
                return View(reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessPayU action for reservation ID: {ReservationId}", id);
                TempData["ErrorMessage"] = "An error occurred while processing your payment. Please try again.";
                return RedirectToAction(nameof(Create));
            }
        }

        // GET: /Reservation/DirectPayU/{id}
        public async Task<IActionResult> DirectPayU(int id)
        {
            _logger.LogInformation("DirectPayU action called for reservation ID: {ReservationId}", id);

            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            // Generate a new transaction ID
            var txnid = Guid.NewGuid().ToString();
            _logger.LogInformation("Generated new transaction ID for direct payment: {TransactionId}", txnid);

            // Save the transaction ID
            reservation.PaymentTxnId = txnid;
            _context.Update(reservation);
            await _context.SaveChangesAsync();

            // Prepare PayU form with auto-submit
            var payUParams = new Dictionary<string, string>
            {
                { "key", _payUService.MerchantKey },
                { "txnid", txnid },
                { "amount", "500.00" },
                { "productinfo", $"Reservation_{reservation.Id}" },
                { "firstname", reservation.Name ?? string.Empty },
                { "email", reservation.Email ?? string.Empty },
                { "phone", reservation.PhoneNumber ?? string.Empty },
                { "surl", "http://localhost:5002/Reservation/PaymentSuccess/" + reservation.Id },
                { "furl", "http://localhost:5002/Reservation/PaymentFailure/" + reservation.Id }
            };

            string hash = _payUService.GenerateHash(payUParams);
            string payuForm = _payUService.GetPayUForm(payUParams, hash, true);
            ViewBag.PayUForm = payuForm;

            return View(reservation);
        }
    }
}
