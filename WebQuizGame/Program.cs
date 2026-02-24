using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebQuizGame;
using WebQuizGame.Classes.Models;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<Authorization>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:5237/")
});

builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();