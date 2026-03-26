using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QuizApp.Client;
using QuizApp.Client.Organizer;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = ResolveApiBaseAddress(builder);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<OrganizerQuizLocalStore>();

await builder.Build().RunAsync();

static Uri ResolveApiBaseAddress(WebAssemblyHostBuilder builder)
{
    var configuredUrl = builder.Configuration["ApiBaseUrl"];
    if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
    {
        return configuredUri;
    }

    return new Uri(builder.HostEnvironment.BaseAddress);
}
