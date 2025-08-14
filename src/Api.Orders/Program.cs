using Api.Orders.Adapters;
using Api.Orders.Services;
using FluentValidation;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Domain.Orders;
using Microsoft.Xrm.Sdk.Messages;
using System.ServiceModel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
	.AddNewtonsoftJson(options =>
	{
		options.SerializerSettings.DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat;
		options.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
	});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new() { 
		Title = "Orders API", 
		Version = "v1",
		Description = "API for managing orders with shared validation between Dataverse and API"
	});
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderValidator>();

// Configure Dataverse connection
builder.Services.AddScoped<ServiceClient>(serviceProvider =>
{
	var configuration = serviceProvider.GetRequiredService<IConfiguration>();
	var connectionString = configuration.GetConnectionString("Dataverse");
    
	if (string.IsNullOrEmpty(connectionString))
	{
		throw new InvalidOperationException("Dataverse connection string is required. " +
			"Please configure 'ConnectionStrings:Dataverse' in appsettings.json or user secrets.");
	}
    
	var logger = serviceProvider.GetRequiredService<ILogger<ServiceClient>>();
	logger.LogInformation("Initializing Dataverse ServiceClient");
    
	return new ServiceClient(connectionString);
});

// Register domain services
builder.Services.AddScoped<IOrderRulesData, DataverseOrderRulesDataForApp>();
builder.Services.AddScoped<IOrderService, DataverseOrderService>();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddApplicationInsights();

// Add health checks
builder.Services.AddHealthChecks()
	.AddCheck<DataverseHealthCheck>("dataverse");

// Add CORS if needed
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowedOrigins", policy =>
	{
		policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
			  .AllowAnyMethod()
			  .AllowAnyHeader();
	});
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API V1");
		c.RoutePrefix = string.Empty; // Serve Swagger at root
	});
	app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

if (app.Configuration.GetSection("Cors:AllowedOrigins").Exists())
{
	app.UseCors("AllowedOrigins");
}

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Add a simple endpoint to test the API
app.MapGet("/api/health", () => new { 
	Status = "Healthy", 
	Timestamp = DateTime.UtcNow,
	Environment = app.Environment.EnvironmentName 
});

try
{
	app.Logger.LogInformation("Starting Orders API");
	app.Run();
}
catch (Exception ex)
{
	app.Logger.LogCritical(ex, "Orders API terminated unexpectedly");
	throw;
}

/// <summary>
/// Health check for Dataverse connectivity
/// </summary>
public class DataverseHealthCheck : IHealthCheck
{
	private readonly ServiceClient _serviceClient;
	private readonly ILogger<DataverseHealthCheck> _logger;

	public DataverseHealthCheck(ServiceClient serviceClient, ILogger<DataverseHealthCheck> logger)
	{
		_serviceClient = serviceClient;
		_logger = logger;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogDebug("Checking Dataverse connectivity");
            
			// Simple connectivity test
			// Simple check: attempt to retrieve at least one attribute from organization entity
			var org = await _serviceClient.RetrieveAsync("organization", Guid.Empty, new Microsoft.Xrm.Sdk.Query.ColumnSet(false));
			_logger.LogDebug("Dataverse connectivity check passed (organization retrieved)");
			return HealthCheckResult.Healthy("Dataverse is accessible");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Dataverse connectivity check failed");
			return HealthCheckResult.Unhealthy("Dataverse is not accessible", ex);
		}
	}
}
