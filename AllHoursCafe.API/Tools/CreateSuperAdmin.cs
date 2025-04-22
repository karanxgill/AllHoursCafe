using AllHoursCafe.API.Data;
using AllHoursCafe.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AllHoursCafe.API.Tools
{
    public class CreateSuperAdmin
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting SuperAdmin check and creation utility...");

            try
            {
                // Create a service collection
                var services = new ServiceCollection();

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Get connection string from args or use default
                string connectionString = args.Length > 0 
                    ? args[0] 
                    : "server=localhost;port=3306;database=allhourscafe_db;user=root;password=password";

                // Add DbContext
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 32))));

                // Build service provider
                var serviceProvider = services.BuildServiceProvider();

                // Get DbContext
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<CreateSuperAdmin>>();

                // Check if SuperAdmin exists
                var superAdminExists = await context.Users.AnyAsync(u => u.Role == "SuperAdmin");

                if (superAdminExists)
                {
                    Console.WriteLine("SuperAdmin user already exists in the database.");
                    var superAdmin = await context.Users.FirstOrDefaultAsync(u => u.Role == "SuperAdmin");
                    Console.WriteLine($"SuperAdmin details: Name={superAdmin.FullName}, Email={superAdmin.Email}");
                }
                else
                {
                    Console.WriteLine("No SuperAdmin user found. Creating SuperAdmin user...");

                    // Create super admin user
                    var superAdminUser = new User
                    {
                        FullName = "SuperAdmin",
                        Email = "superadmin@allhourscafe.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("SuperAdmin@123"), // Default password: SuperAdmin@123
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        Role = "SuperAdmin"
                    };

                    // Add super admin user to the database
                    await context.Users.AddAsync(superAdminUser);
                    await context.SaveChangesAsync();

                    Console.WriteLine($"SuperAdmin user created successfully with email: {superAdminUser.Email}");
                    Console.WriteLine("Default password: SuperAdmin@123");
                    Console.WriteLine("IMPORTANT: Change this password after first login!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("SuperAdmin check and creation utility completed.");
        }
    }
}
