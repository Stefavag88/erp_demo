using ERP.CustomerCustomFields.Api.Authentication;
using ERP.CustomerCustomFields.Api.Endpoints;
using ERP.CustomerCustomFields.Api.Errors;
using ERP.CustomerCustomFields.Application.Contracts;
using ERP.CustomerCustomFields.Application.Repositories;
using ERP.CustomerCustomFields.Application.Services;
using ERP.CustomerCustomFields.Infrastructure.Identifiers;
using ERP.CustomerCustomFields.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;

namespace ERP.CustomerCustomFields.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connection = builder.Configuration.GetConnectionString("CustomerCustomFields");
        if (string.IsNullOrWhiteSpace(connection))
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
        builder.Services.AddScoped<ICustomerRepository>(_ => new DapperCustomerRepository(connection));
        builder.Services.AddScoped<ICustomFieldRepository>(_ => new DapperCustomFieldRepository(connection));
        builder.Services.AddScoped<CustomerService>();
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
