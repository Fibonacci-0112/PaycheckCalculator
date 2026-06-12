using PaycheckCalc.Blazor.Components;
using PaycheckCalc.Blazor.Services;
using PaycheckCalc.Core.DependencyInjection;
using PaycheckCalc.Shared.Client;
using PaycheckCalc.Shared.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var taxDataPath = Path.Combine(AppContext.BaseDirectory, "TaxData");
builder.Services.AddPaycheckCalcCore(new FileSystemTaxDataReader(taxDataPath));

// Account + paycheck sync. The session store and account session are scoped to the Blazor circuit, so
// anonymous data lives only until the browser tab closes; signing in syncs it to the API server-side.
builder.Services.AddScoped<CircuitAccountSession>();
builder.Services.AddScoped<ITokenStore>(sp => sp.GetRequiredService<CircuitAccountSession>());
builder.Services.AddSingleton<IApiBaseAddressProvider, ConfigApiBaseAddressProvider>();
builder.Services.AddScoped<SessionPaycheckStore>();
builder.Services.AddScoped<ISavedPaycheckStore>(sp => sp.GetRequiredService<SessionPaycheckStore>());
builder.Services.AddHttpClient<PaycheckApiClient>();
builder.Services.AddScoped<PaycheckSyncService>();

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
