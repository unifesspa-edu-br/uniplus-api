namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

public sealed class DocumentoDoContratoPublicadoTests
{
    [Fact(DisplayName = "O registro de um documento o torna conhecido pelo nome, e só ele")]
    public void RegistroDoDocumento_TornaConhecidoSoOProprioNome()
    {
        using ServiceProvider provedor = ProvedorComDocumento("selecao");
        IServiceProviderIsKeyedService chaves = provedor.GetRequiredService<IServiceProviderIsKeyedService>();

        chaves.IsKeyedService(typeof(IOpenApiDocumentProvider), "selecao").Should().BeTrue();
        chaves.IsKeyedService(typeof(IOpenApiDocumentProvider), "outro").Should().BeFalse();
    }

    [Fact(DisplayName = "O nome de um documento publicado casa com a rota")]
    public void NomePublicado_Casa()
    {
        using ServiceProvider provedor = ProvedorComDocumento("selecao");

        Casa(provedor, "selecao").Should().BeTrue();
    }

    [Fact(DisplayName = "O nome de um documento que o serviço não publica não casa com a rota")]
    public void NomeNaoPublicado_NaoCasa()
    {
        using ServiceProvider provedor = ProvedorComDocumento("selecao");

        Casa(provedor, "inexistente").Should().BeFalse();
    }

    [Fact(DisplayName = "O nome publicado em outra grafia não casa com a rota")]
    public void NomePublicadoEmOutraGrafia_NaoCasa()
    {
        using ServiceProvider provedor = ProvedorComDocumento("selecao");

        Casa(provedor, "SELECAO").Should().BeFalse();
    }

    [Fact(DisplayName = "Sem contexto da requisição, o nome não casa com a rota")]
    public void SemContextoDaRequisicao_NaoCasa()
    {
        RouteValueDictionary valores = new() { ["documentName"] = "selecao" };

        new DocumentoDoContratoPublicado()
            .Match(httpContext: null, route: null, "documentName", valores, RouteDirection.IncomingRequest)
            .Should().BeFalse();
    }

    [Fact(DisplayName = "Sem o valor do parâmetro na rota, o nome não casa")]
    public void SemOValorDoParametro_NaoCasa()
    {
        using ServiceProvider provedor = ProvedorComDocumento("selecao");
        DefaultHttpContext contexto = new() { RequestServices = provedor };

        new DocumentoDoContratoPublicado()
            .Match(contexto, route: null, "documentName", [], RouteDirection.IncomingRequest)
            .Should().BeFalse();
    }

    private static bool Casa(ServiceProvider provedor, string nome)
    {
        DefaultHttpContext contexto = new() { RequestServices = provedor };
        RouteValueDictionary valores = new() { ["documentName"] = nome };

        return new DocumentoDoContratoPublicado()
            .Match(contexto, route: null, "documentName", valores, RouteDirection.IncomingRequest);
    }

    private static ServiceProvider ProvedorComDocumento(string nome)
    {
        ServiceCollection servicos = new();
        servicos.AddOpenApi(nome);
        return servicos.BuildServiceProvider();
    }
}
