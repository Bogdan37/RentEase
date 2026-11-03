using Microsoft.EntityFrameworkCore;
using RentEase.Data;
using RentEase.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IPropertySearchService, PropertySearchService>();
builder.Services.AddSingleton<IMessageProducer, RabbitMqProducer>();
builder.Services.AddHostedService<BookingNotificationConsumer>();

builder.Services.AddDbContext<RentEaseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "RentEase_";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RentEaseDbContext>();
    var searchService = scope.ServiceProvider.GetRequiredService<IPropertySearchService>();
    
    var allProperties = db.Properties.AsNoTracking().ToList();
    searchService.IndexProperties(allProperties);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultControllerRoute();
app.Run();