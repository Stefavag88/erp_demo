using ERP.Discounts.Api.Endpoints;
using ERP.Discounts.Api.Errors;
using ERP.Discounts.Api.Serialization;
using ERP.Discounts.Application;
using ERP.Discounts.Application.Contracts;
using ERP.Discounts.Application.Providers;
using ERP.Discounts.Application.Repositories;
using ERP.Discounts.Domain;
using ERP.Discounts.Infrastructure.Data;
using ERP.Discounts.Infrastructure.Repositories;
using ERP.Discounts.Infrastructure.Providers;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Discounts");

if (string.IsNullOrWhiteSpace(connectionString)) 
    throw new InvalidOperationException("Configure ConnectionStrings:Discounts before starting the API.");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new EuroAmountJsonConverter());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PricingValidationExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));

//repositories
builder.Services.AddScoped<IPriceListRepository, DapperPriceListRepository>();
builder.Services.AddScoped<IPromotionRepository, DapperPromotionRepository>();
builder.Services.AddScoped<ICouponRepository, DapperCouponRepository>();

//providers
builder.Services.AddSingleton<ICustomerSegmentProvider, DummyCustomerSegmentProvider>();
builder.Services.AddScoped<IDiscountProvider, PriceListDiscountProvider>();
builder.Services.AddScoped<IDiscountProvider, PromotionDiscountProvider>();
builder.Services.AddScoped<IDiscountProvider, CouponDiscountProvider>();

builder.Services.AddScoped<IDiscountEngine, DiscountEngine>();
builder.Services.AddSingleton<DiscountCalculator>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapPricingEndpoints();
app.Run();

public partial class Program { }
