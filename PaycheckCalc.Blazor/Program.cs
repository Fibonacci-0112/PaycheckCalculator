using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var taxDataPath = Path.Combine(AppContext.BaseDirectory, "TaxData");
builder.Services.AddPaycheckCalcCore(new FileSystemTaxDataReader(taxDataPath));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
