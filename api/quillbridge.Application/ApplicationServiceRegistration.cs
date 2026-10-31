using FluentValidation;
using Microsoft.Extensions.Configuration;
using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using quillbridge.Application.Services;
using quillbridge.Application.Services.IServices;

namespace quillbridge.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));
        services.AddSingleton<IAiGuideWriter, ClaudeGuideWriter>();
        services.AddSingleton<BudgetGuard>();
        services.AddSingleton<GuideBatchService>();
        services.AddScoped<IGuideService, GuideService>();

        services.AddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProspectService, ProspectService>();

        return services;
    }
}
