using BingoSim.Application.Validators;
using BingoSim.Infrastructure;
using BingoSim.Infrastructure.Persistence;
using BingoSim.Web.Components;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BingoSim.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add Infrastructure services (DbContext, Repositories, Application Services)
        builder.Services.AddInfrastructure(builder.Configuration);

        // Add FluentValidation validators
        builder.Services.AddValidatorsFromAssemblyContaining<CreatePlayerProfileRequestValidator>();

        var app = builder.Build();

        // Apply migrations automatically in development
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }
}