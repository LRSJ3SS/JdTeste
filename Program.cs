using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UABEA.Web;
using UABEA.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient apontando para a própria origem do app — usado para buscar
// arquivos estáticos de wwwroot (ex.: classdata.tpk) via fetch, já que
// no WASM não existe acesso direto a disco.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Serviço central: encapsula o AssetsManager (o mesmo que o UABEA
// desktop usa) e o estado do workspace atualmente aberto.
builder.Services.AddScoped<AssetWorkspaceService>();

await builder.Build().RunAsync();
