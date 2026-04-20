using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebQuizGame;
using WebQuizGame.Classes.Models;
using Blazored.LocalStorage;
using WebQuizGame.Classes.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<Authorization>();
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddScoped<MultiplayerClient>();
builder.Services.AddScoped<RankedMultiplayerClient>();
builder.Services.AddScoped<CustomQuizMultiplayerClient>();
builder.Services.AddScoped<UnifiedLobbyJoinService>();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();