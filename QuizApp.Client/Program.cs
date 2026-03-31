using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QuizApp.Client;
using QuizApp.Client.Organizer;
using QuizApp.Client.Team;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = ResolveApiBaseAddress(builder);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = apiBaseAddress });
builder.Services.AddScoped<OrganizerQuizLocalStore>();
builder.Services.AddScoped<TeamSessionLocalStore>();
builder.Services.AddScoped<ActiveSessionState>();

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
