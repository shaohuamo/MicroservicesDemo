using CommonService.Middlewares;
using Microsoft.AspNetCore.HttpLogging;
using ProductsMicroservice.Core;
using ProductsMicroservice.Infrastructure;
using ProductsMicroService.API.Extensions;
using ProductsMicroService.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

//Add Observability
builder.AddObservability();

// Add services to the container.
builder.Services.AddProductsMicroserviceCore(builder.Configuration);
builder.Services.ProductsMicroserviceInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    // allow action method names to end with "Async" without removing "Async" suffix in route template
    options.SuppressAsyncSuffixInActionNames = false;
});

//Add Swagger services
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        //generate api.xml by comment of action method
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "api.xml"));
    });
}

builder.Services.ConfigureHttpClientDefaults(http =>
{
    //configure default resilience policies(retry, circuit breaker,timeout) for all HttpClients
    http.AddStandardResilienceHandler();
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestProperties | HttpLoggingFields.ResponsePropertiesAndHeaders;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandlingMiddleware();
app.TraceContextMiddleware();

//Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => {

        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProductsMicroService.API");
        //set swagger as root path
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpLogging();

app.MapControllers();

app.Run();
