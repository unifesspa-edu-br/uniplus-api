namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>Gera o documento OpenAPI pelo pipeline do Uni+, como o host o publica.</summary>
internal static class GeradorDeDocumentoDeTeste
{
    /// <summary>O documento das rotas que <paramref name="rotas"/> mapeia.</summary>
    public static async Task<DocumentoPublicado> GerarAsync(Action<WebApplication> rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        string documento = $"codigos-{Guid.NewGuid():N}";
        WebApplicationBuilder builder = NovoConstrutor(documento);
        await using WebApplication app = builder.Build();
        rotas(app);
        await app.StartAsync();

        return await PublicarAsync(app, documento);
    }

    /// <summary>O documento das rotas que <paramref name="rotas"/> mapeia, gerado duas vezes pela mesma aplicação.</summary>
    public static async Task<(DocumentoPublicado Primeira, DocumentoPublicado Segunda)> GerarDuasVezesAsync(Action<WebApplication> rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        string documento = $"codigos-{Guid.NewGuid():N}";
        await using WebApplication app = NovoConstrutor(documento).Build();
        rotas(app);
        await app.StartAsync();

        return (await PublicarAsync(app, documento), await PublicarAsync(app, documento));
    }

    /// <summary>O documento das ações de <see cref="ControladorDeCodigosDeTeste"/>.</summary>
    public static async Task<DocumentoPublicado> GerarDosControladoresAsync()
    {
        await using WebApplication app = NovoConstrutorComControladores(ControladorDeCodigosDeTeste.Documento).Build();
        app.MapControllers();
        await app.StartAsync();

        return await PublicarAsync(app, ControladorDeCodigosDeTeste.Documento);
    }

    /// <summary>
    /// Um construtor com o contrato do Uni+ publicado no documento <paramref name="documento"/>. A configuração
    /// <paramref name="antes"/> do documento, quando há, entra antes da do Uni+: seus transformers rodam primeiro.
    /// </summary>
    public static WebApplicationBuilder NovoConstrutor(string documento, Action<OpenApiOptions>? antes = null) =>
        Montar(WebApplication.CreateBuilder(), documento, antes);

    /// <summary>Um construtor como <see cref="NovoConstrutor"/>, com os controladores deste assembly de teste.</summary>
    public static WebApplicationBuilder NovoConstrutorComControladores(string documento)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(ControladorDeCodigosDeTeste).Assembly);

        return Montar(builder, documento, antes: null);
    }

    private static WebApplicationBuilder Montar(WebApplicationBuilder builder, string documento, Action<OpenApiOptions>? antes)
    {
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddUniPlusJsonOptions();

        if (antes is not null)
        {
            builder.Services.Configure(documento, antes);
        }

        builder.Services.AddUniPlusOpenApi(documento, builder.Configuration);

        return builder;
    }

    /// <summary>O documento <paramref name="documento"/> de uma aplicação já iniciada.</summary>
    public static async Task<DocumentoPublicado> PublicarAsync(WebApplication app, string documento)
    {
        ArgumentNullException.ThrowIfNull(app);

        OpenApiDocument gerado = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(documento).GetOpenApiDocumentAsync();

        return new DocumentoPublicado(gerado);
    }
}
