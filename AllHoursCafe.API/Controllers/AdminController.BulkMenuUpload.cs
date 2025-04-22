using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using OfficeOpenXml; // EPPlus

namespace AllHoursCafe.API.Controllers
{
    [Authorize(Roles = "Admin")]
    public partial class AdminController : Controller
    {
        [HttpPost]
        public async Task<IActionResult> BulkUploadMenuItems(IFormFile excelFile)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "No file selected.";
                return RedirectToAction("MenuItems");
            }

            var menuItems = new List<MenuItem>();
            try
            {
                using (var stream = new MemoryStream())
                {
                    await excelFile.CopyToAsync(stream);
                    stream.Position = 0;
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        int rowCount = worksheet.Dimension.Rows;
                        for (int row = 2; row <= rowCount; row++)
                        {
                            var item = new MenuItem
                            {
                                Name = worksheet.Cells[row, 1].Text,
                                CategoryId = GetCategoryIdByName(worksheet.Cells[row, 2].Text),
                                Price = decimal.TryParse(worksheet.Cells[row, 3].Text, out var price) ? price : 0,
                                Description = worksheet.Cells[row, 4].Text,
                                ImageUrl = ProcessImageUrl(worksheet.Cells[row, 5].Text),
                                IsActive = worksheet.Cells[row, 6].Text.ToLower() == "true",
                                Calories = int.TryParse(worksheet.Cells[row, 7].Text, out var cal) ? cal : 0,
                                PrepTimeMinutes = int.TryParse(worksheet.Cells[row, 8].Text, out var prep) ? prep : 0,
                                IsVegetarian = worksheet.Cells[row, 9].Text.ToLower() == "true",
                                IsVegan = worksheet.Cells[row, 10].Text.ToLower() == "true",
                                IsGlutenFree = worksheet.Cells[row, 11].Text.ToLower() == "true",
                                SpicyLevel = int.TryParse(worksheet.Cells[row, 12].Text, out var spicy) ? spicy.ToString() : "0"
                            };
                            if (!string.IsNullOrWhiteSpace(item.Name) && item.CategoryId > 0)
                                menuItems.Add(item);
                        }
                    }
                }
                _context.MenuItems.AddRange(menuItems);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Successfully uploaded {menuItems.Count} menu items.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Bulk upload failed: " + ex.Message;
            }
            return RedirectToAction("MenuItems");
        }

        private int GetCategoryIdByName(string categoryName)
        {
            var cat = _context.Categories.FirstOrDefault(c => c.Name.ToLower() == categoryName.ToLower());
            return cat?.Id ?? 0;
        }

        private string ProcessImageUrl(string imageUrl)
        {
            // If the URL is empty, return a default image
            if (string.IsNullOrEmpty(imageUrl))
            {
                return "/images/placeholder.jpg";
            }

            // If it's an external URL (starts with http:// or https://), keep it as is
            if (imageUrl.StartsWith("http://") || imageUrl.StartsWith("https://"))
            {
                return imageUrl;
            }

            // If it's already a local path, keep it as is
            if (imageUrl.StartsWith("/images/"))
            {
                return imageUrl;
            }

            // Otherwise, treat it as a filename and construct a local path
            return $"/images/Items/other/{imageUrl}";
        }
    }
}
