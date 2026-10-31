using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using quillbridge.Application.Interfaces;
using quillbridge.Domain.Models;
using quillbridge.Persistence.AppDbContext;
using quillbridge.Persistence.Repositories;

namespace quillbridge.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuillbridgeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<QuillbridgeDbContext>();

        services.AddScoped<IProspectRepository, ProspectRepository>();
        services.AddScoped<IGuideRepository, GuideRepository>();

        return services;
    }
}
