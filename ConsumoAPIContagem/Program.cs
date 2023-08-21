using Microsoft.Extensions.Configuration;
using Serilog;
using ConsumoAPIContagem.Clients;

// Projeto com as API utilizada nos testes (.NET 7 + ASP.NET Core):
// https://github.com/renatogroffe/ASPNETCore7-REST_API-ADB2C-HttpFiles_ContagemAcessos
var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile($"appsettings.json");
var config = builder.Build();


var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
logger.Information("***** Testes com Azure AD B2C + API REST + Microsoft.Identity.Client *****");

var apiContagemClient = new APIContagemClient(config, logger);
await apiContagemClient.Autenticar();
while (true)
{
    await apiContagemClient.ExibirResultadoContador();
    await Console.Out.WriteLineAsync(
        "Pressione qualquer tecla para continuar...");
    Console.ReadKey();
}