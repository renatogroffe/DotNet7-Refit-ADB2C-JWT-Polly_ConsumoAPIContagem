using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Refit;
using ConsumoAPIContagem.Extensions;
using ConsumoAPIContagem.Interfaces;
using ConsumoAPIContagem.Models;
using Microsoft.Identity.Client;

namespace ConsumoAPIContagem.Clients;

public class APIContagemClient
{
    private IContagemAPI _contagemAPI;
    private IConfiguration _configuration;
    private Serilog.Core.Logger _logger;
    private Token? _token;
    private readonly IConfidentialClientApplication _confidentialApp;
    private readonly string[] _scopes;
    private AsyncRetryPolicy _jwtPolicy;

    public bool IsAuthenticatedUsingToken
    {
        get => _token?.Authenticated ?? false;
    }

    public APIContagemClient(IConfiguration configuration,
        Serilog.Core.Logger logger)
    {
        _configuration = configuration;
        _logger = logger;

        string urlBase = _configuration.GetSection(
            "APIContagem:UrlBase").Value!;

        _contagemAPI = RestService.For<IContagemAPI>(urlBase);

        var tenantName = configuration["ADB2C:TenantName"];
        var tenant = $"{tenantName}.onmicrosoft.com";
        var azureAdB2CHostname = $"{tenantName}.b2clogin.com";
        var policySignUpSignIn = configuration["ADB2C:PolicySignUpSignIn"]!;
        var authorityBase = $"https://{azureAdB2CHostname}/tfp/{tenant}/";
        var authoritySignUpSignIn = $"{authorityBase}{policySignUpSignIn}";
        _confidentialApp = ConfidentialClientApplicationBuilder.Create(configuration["ADB2C:ConsumerAppId"])
            .WithClientSecret(configuration["ADB2C:ConsoleAppSecret"])
            .WithB2CAuthority(authoritySignUpSignIn)
            .Build();
        _scopes = new string[] { $"https://{tenantName}.onmicrosoft.com/{configuration["ADB2C:ApiContagemAppId"]}/.default openid offline_access" };

        _jwtPolicy = CreateAccessTokenPolicy();
    }

    public async Task Autenticar()
    {
        try
        {
            _token = null;

            // Envio da requisição a fim de autenticar
            // e obter o token de acesso
            var resultAuthApi01 = await _confidentialApp.AcquireTokenForClient(_scopes).ExecuteAsync();
            if (!String.IsNullOrWhiteSpace(resultAuthApi01?.AccessToken))
                _token = new Token() { AccessToken = resultAuthApi01.AccessToken };
            
            Console.Out.WriteLine();
            Console.Out.WriteLine(
                Environment.NewLine +
                JsonSerializer.Serialize(_token));
        }
        catch (Exception ex)
        {
            
            _logger.Error($"Falha ao autenticar... | {ex.Message}");
        }
    }

    private AsyncRetryPolicy CreateAccessTokenPolicy()
    {
        return Policy
            .HandleInner<ApiException>(
                ex => ex.StatusCode == HttpStatusCode.Unauthorized)
            .RetryAsync(1, async (ex, retryCount, context) =>
            {
                var corAnterior = Console.ForegroundColor;

                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Out.WriteLineAsync(
                    Environment.NewLine + "Token expirado ou usuário sem permissão!");
                Console.ForegroundColor = corAnterior;

                Console.ForegroundColor = ConsoleColor.Green;
                await Console.Out.WriteLineAsync(
                    Environment.NewLine + "Execução de RetryPolicy...");
                Console.ForegroundColor = corAnterior;

                await Autenticar();
                if (!(_token?.Authenticated ?? false))
                    throw new InvalidOperationException("Token inválido!");

                context["AccessToken"] = _token.AccessToken;
            });
    }

    public async Task ExibirResultadoContador()
    {
        var retorno = await _jwtPolicy.ExecuteWithTokenAsync<ResultadoContador>(
            _token!, async (context) =>
        {
            var resultado = await _contagemAPI.ObterValorAtualAsync(
              $"Bearer {context["AccessToken"]}");
            return resultado;
        });

        Console.Out.WriteLine();
        _logger.Information("Retorno da API de Contagem: " +
            JsonSerializer.Serialize(retorno));
    }
}