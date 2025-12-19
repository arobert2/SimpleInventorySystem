using SimpleInventorySystem.Web.Components;
using SimpleInventorySystem.Shared.Services;
using SimpleInventorySystem.Web.Services;
using Microsoft.EntityFrameworkCore;
using SimpleInventorySystem.Database;
using SimpleInventorySystem.Database.Contracts;
using System.Data;
using Dapper;
using Npgsql;
using SimpleInventorySystem.Web.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<DbConnectionOptions>()
    .BindConfiguration(DbConnectionOptions.CONFIG_SECTION_NAME)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<IDbConnection>(sp => new NpgsqlConnection(sp.CreateScope().ServiceProvider.GetRequiredService<IOptions<DbConnectionOptions>>().Value.GetDbConnectionString()));
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();

// Add device-specific services used by the SimpleInventorySystem.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var scope = app.Services.CreateScope();
var invRepo = scope.ServiceProvider.GetRequiredService<IInventoryRepository>() as InventoryRepository;
var dbOpt = scope.ServiceProvider.GetRequiredService<IOptions<DbConnectionOptions>>().Value;

invRepo!.CreateDatabase(dbOpt.Database, new NpgsqlConnection(dbOpt.GetPostgresConnectionString()));
invRepo!.CreateInventoryTable();


app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(SimpleInventorySystem.Shared._Imports).Assembly);

app.Run();
