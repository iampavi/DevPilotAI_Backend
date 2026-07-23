using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Persistence;

public class ApplicationDbContextInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<ApplicationDbContextInitializer> _logger;

    public ApplicationDbContextInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<ApplicationDbContextInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        _logger.LogInformation("Checking database seeding status...");

        // 1. Seed Roles
        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new ApplicationRole("Admin"));
            _logger.LogInformation("Seeded 'Admin' role.");
        }
        if (!await _roleManager.RoleExistsAsync("User"))
        {
            await _roleManager.CreateAsync(new ApplicationRole("User"));
            _logger.LogInformation("Seeded 'User' role.");
        }

        // 2. Seed Default System User
        var systemUserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F");
        var systemUser = await _userManager.FindByIdAsync(systemUserId.ToString());
        if (systemUser == null)
        {
            _logger.LogInformation("Seeding default system user...");
            systemUser = new ApplicationUser
            {
                Id = systemUserId,
                UserName = "system@devpilot.ai",
                Email = "system@devpilot.ai",
                FirstName = "System",
                LastName = "User",
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(systemUser, "SystemSecurePassword123!");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(systemUser, "User");
                await _userManager.AddToRoleAsync(systemUser, "Admin");
                _logger.LogInformation("Seeded system user successfully.");
            }
            else
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to seed system user: {Errors}", errors);
            }
        }

        // 3. Seed Default Workspace (and assign to System User)
        if (!await _context.Workspaces.AnyAsync())
        {
            _logger.LogInformation("Seeding default workspace...");

            var defaultWorkspace = new Workspace
            {
                Id = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21E"),
                Name = "Default Workspace",
                Description = "Default system-generated workspace for early projects.",
                UserId = systemUserId
            };

            _context.Workspaces.Add(defaultWorkspace);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Default workspace seeded successfully.");
        }
    }
}
