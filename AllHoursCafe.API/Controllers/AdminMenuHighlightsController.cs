using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace AllHoursCafe.API.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")] // Only users with Admin or SuperAdmin role can access this controller
    public class AdminMenuHighlightsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminMenuHighlightsController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminMenuHighlightsController(
            ApplicationDbContext context,
            ILogger<AdminMenuHighlightsController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: AdminMenuHighlights
        public async Task<IActionResult> Index()
        {
            var highlights = await _context.MenuHighlights
                .Include(h => h.MenuItem)
                .ThenInclude(m => m.Category)
                .OrderBy(h => h.Section)
                .ThenBy(h => h.DisplayOrder)
                .ToListAsync();

            ViewBag.MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View(highlights);
        }

        // GET: AdminMenuHighlights/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View();
        }

        // POST: AdminMenuHighlights/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuHighlight highlight, IFormFile ImageFile)
        {
            _logger.LogWarning("Create POST action called");
            // Remove ImageFile required error if present (from model binding or validation attributes)
            if (ModelState.ContainsKey("ImageFile"))
            {
                ModelState["ImageFile"].Errors.Clear();
            }
            ModelState.Remove("ImageFile");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle image upload if provided
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // Create a unique filename
                        var fileName = Path.GetFileName(ImageFile.FileName);
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;

                        // Create directory if it doesn't exist
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "highlights");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Save the file
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(fileStream);
                        }

                        // Update the CustomImageUrl property
                        highlight.CustomImageUrl = $"/images/highlights/{uniqueFileName}";
                    }
                    // If no file was uploaded but a URL was provided, keep the URL as is

                    _context.Add(highlight);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Menu highlight created successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating menu highlight");
                    ModelState.AddModelError("", "Unable to create menu highlight. Please try again.");
                }
            }

            // Log model state errors for debugging
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key].Errors;
                foreach (var error in errors)
                {
                    _logger.LogWarning($"ModelState error for '{key}': {error.ErrorMessage}");
                }
            }

            ViewBag.MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View(highlight);
        }

        // GET: AdminMenuHighlights/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var highlight = await _context.MenuHighlights
                .Include(h => h.MenuItem)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (highlight == null)
            {
                return NotFound();
            }

            ViewBag.MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            return View(highlight);
        }

        // POST: AdminMenuHighlights/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MenuItemId,DisplayOrder,Section,CustomTitle,CustomDescription,CustomImageUrl,IsActive")] MenuHighlight highlight, IFormFile ImageFile)
        {
            _logger.LogInformation($"Edit POST method called for highlight ID: {id}");

            if (id != highlight.Id)
            {
                _logger.LogWarning($"ID mismatch: URL ID {id} != highlight.Id {highlight.Id}");
                return NotFound();
            }

            // Log model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid:");
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Count > 0)
                    {
                        _logger.LogWarning($"- {state.Key}: {string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage))}");
                    }
                }
            }

            _logger.LogInformation($"Highlight data: MenuItemId={highlight.MenuItemId}, Section={highlight.Section}, DisplayOrder={highlight.DisplayOrder}, IsActive={highlight.IsActive}");
            _logger.LogInformation($"Image data: CustomImageUrl={highlight.CustomImageUrl ?? "null"}, ImageFile={ImageFile?.FileName ?? "null"}");

            // Remove ModelState error for ImageFile if not uploading a new image and CustomImageUrl is present
            if (ImageFile == null && !string.IsNullOrEmpty(highlight.CustomImageUrl))
            {
                ModelState.Remove("ImageFile");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the current highlight to check if we need to preserve the existing image URL
                    var existingHighlight = await _context.MenuHighlights.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
                    if (existingHighlight == null)
                    {
                        _logger.LogWarning($"Existing highlight with ID {id} not found in database");
                        return NotFound();
                    }

                    string currentImageUrl = existingHighlight.CustomImageUrl;
                    _logger.LogInformation($"Current image URL: {currentImageUrl ?? "null"}");

                    // Handle image upload if provided
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        _logger.LogInformation($"Processing uploaded image: {ImageFile.FileName}, size: {ImageFile.Length} bytes");

                        // Create a unique filename
                        var fileName = Path.GetFileName(ImageFile.FileName);
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + fileName;

                        // Create directory if it doesn't exist
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "highlights");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            _logger.LogInformation($"Creating directory: {uploadsFolder}");
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        _logger.LogInformation($"Saving file to: {filePath}");

                        // Save the file
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(fileStream);
                        }

                        // Update the CustomImageUrl property
                        highlight.CustomImageUrl = $"/images/highlights/{uniqueFileName}";
                        _logger.LogInformation($"Updated CustomImageUrl to: {highlight.CustomImageUrl}");
                    }
                    else if (string.IsNullOrEmpty(highlight.CustomImageUrl) && !string.IsNullOrEmpty(currentImageUrl))
                    {
                        // If no new image URL was provided and no file was uploaded, but there was an existing image URL,
                        // keep the existing image URL
                        _logger.LogInformation("No new image provided, keeping existing image URL");
                        highlight.CustomImageUrl = currentImageUrl;
                    }
                    // Otherwise, use the provided CustomImageUrl value (which might be null/empty to clear the image)

                    _logger.LogInformation("Updating highlight in database");
                    _context.Update(highlight);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Database update successful");
                    TempData["SuccessMessage"] = "Menu highlight updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "DbUpdateConcurrencyException occurred");
                    if (!MenuHighlightExists(highlight.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error updating menu highlight");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");

                    // Check if it's an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        _logger.LogInformation("Responding to AJAX request with error JSON");
                        return Json(new {
                            success = false,
                            errorMessage = "An unexpected error occurred. Please try again."
                        });
                    }
                }
            }

            // If we got this far, something failed, redisplay form
            ViewBag.MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Where(m => m.IsActive)
                .OrderBy(m => m.Category.Name)
                .ThenBy(m => m.Name)
                .ToListAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // If AJAX, return JSON error/validation
                var validationErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Json(new
                {
                    success = false,
                    errorMessage = "Please correct the validation errors shown below.",
                    validationErrors
                });
            }

            return View(highlight);
        }

        // GET: AdminMenuHighlights/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var highlight = await _context.MenuHighlights
                .Include(h => h.MenuItem)
                .ThenInclude(m => m.Category)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (highlight == null)
            {
                return NotFound();
            }

            return View(highlight);
        }

        // POST: AdminMenuHighlights/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var highlight = await _context.MenuHighlights.FindAsync(id);
            if (highlight != null)
            {
                _context.MenuHighlights.Remove(highlight);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Menu highlight deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MenuHighlightExists(int id)
        {
            return _context.MenuHighlights.Any(e => e.Id == id);
        }
    }
}
