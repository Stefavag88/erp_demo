using ERP.CustomerCustomFields.Api.Authentication;
using ERP.CustomerCustomFields.Api.Endpoints;
using ERP.CustomerCustomFields.Api.Errors;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Application.Services;
using ERP.CustomerCustomFields.Infrastructure.Data;
using ERP.CustomerCustomFields.Infrastructure.Caching;
using ERP.CustomerCustomFields.Infrastructure.Identifiers;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.CustomerCustomFields.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("CustomerCustomFields");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:CustomerCustomFields.");
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.RespectRequiredConstructorParameters = true;
            o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<CustomFieldExceptionHandler>();
        builder.Services.AddAuthentication("Demo").AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>("Demo", _ =>
        {
        });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ICustomerIdGenerator, CustomerIdGenerator>();
        builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
        builder.Services.AddScoped<ICustomerRepository, DapperCustomerRepository>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<DapperCustomFieldRepository>();
        builder.Services.AddSingleton<ICustomFieldRepository>(services => new CachedCustomFieldRepository(
            services.GetRequiredService<DapperCustomFieldRepository>(),
            services.GetRequiredService<IMemoryCache>()));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor, CurrentActor>();
        builder.Services.AddScoped<ICustomFieldService, CustomFieldService>();
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCustomFieldEndpoints();
        app.Run();
    }
}
