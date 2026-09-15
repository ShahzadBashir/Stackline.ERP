using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Stackline.API.Data;
using Stackline.API.Data.Entities;
using Stackline.API.Features.Auth;
using Stackline.API.Features.Categories;
using Stackline.API.Features.CustomerReceipts;
using Stackline.API.Features.Customers;
using Stackline.API.Features.Inventory;
using Stackline.API.Features.Items;
using Stackline.API.Features.Purchases;
using Stackline.API.Features.Sales;
using Stackline.API.Features.Statements;
using Stackline.API.Features.SupplierPayments;
using Stackline.API.Features.Suppliers;
using Stackline.API.Features.Tenants;
using Stackline.API.Features.Warehouses;
using Stackline.API.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MasterDb")));

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ITenantConnectionResolver, TenantConnectionResolver>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ISaleService, SaleService>();
builder.Services.AddScoped<ICustomerBalanceService, CustomerBalanceService>();
builder.Services.AddScoped<ICustomerReceiptService, CustomerReceiptService>();
builder.Services.AddScoped<ISupplierBalanceService, SupplierBalanceService>();
builder.Services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
builder.Services.AddScoped<IAccountStatementService, AccountStatementService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
    if (tenantContext.IsResolved)
    {
        options.UseNpgsql(tenantContext.ConnectionString);
    }
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

builder.Services.AddCors(option =>
{
    option.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
    });
});

var app = builder.Build();

if (args.Length > 0 && args[0] == "seed-superadmin")
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: dotnet run -- seed-superadmin <email> <password>");
        return;
    }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

    var email = args[1];
    var password = args[2];

    if (await db.GlobalUsers.AnyAsync(u => u.Email == email))
    {
        Console.WriteLine("A user with this email already exists.");
        return;
    }

    db.GlobalUsers.Add(new GlobalUser
    {
        Id = Guid.NewGuid(),
        TenantId = null,               // SuperAdmin belongs to no tenant
        FullName = "Super Admin",
        Email = email,
        PasswordHash = passwordService.HashPassword(password),
        Role = Roles.SuperAdmin
    });

    await db.SaveChangesAsync();
    Console.WriteLine($"SuperAdmin created: {email}");
    return; // exit without starting the web server
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapTenantEndpoints();
app.MapAuthEndpoints();
app.MapWarehouseEndpoints();
app.MapCategoryEndpoints();
app.MapItemEndpoints();
app.MapSupplierEndpoints();
app.MapPurchaseEndpoints();
app.MapInventoryEndpoints();
app.MapCustomerEndpoints();
app.MapSaleEndpoints();
app.MapCustomerReceiptEndpoints();
app.MapSupplierPaymentEndpoints();

app.Run();