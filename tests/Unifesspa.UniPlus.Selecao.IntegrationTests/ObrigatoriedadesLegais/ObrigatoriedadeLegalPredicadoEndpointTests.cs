namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Integration tests da leitura do predicado no POST admin de <c>ObrigatoriedadeLegal</c>,
/// contra Postgres real via Testcontainers.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalPredicadoEndpointTests : ObrigatoriedadeLegalAdminTestBase
{
    public ObrigatoriedadeLegalPredicadoEndpointTests(CascadingFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>
    /// O discriminador do predicado é <c>$tipo</c> (ADR-0058), e não o <c>type</c> que a maioria
    /// das bibliotecas usa por convenção: errar o <c>$</c> é o modo de falha esperado de quem
    /// integra pela primeira vez. Isso devolvia 500 — "erro interno do servidor" para um defeito
    /// do cliente que a API sabe descrever, e num status que endpoint nenhum declara.
    /// </summary>
    [Theory(DisplayName = "POST com predicado sem '$tipo' devolve 400 nomeando o campo e o discriminador")]
    [InlineData("tipo")]
    [InlineData("type")]
    public async Task Criar_PredicadoSemDiscriminador_Retorna400NomeandoOCampo(string chaveErrada)
    {
        using HttpClient client = await ClienteComConcessaoAsync();
        Dictionary<string, object> predicadoSemDiscriminador = PredicadoSemDiscriminador(chaveErrada);

        HttpResponseMessage response = await PostAsync(
            client,
            "/api/selecao/admin/obrigatoriedades-legais",
            PayloadCom(predicadoSemDiscriminador));

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"body: {body}");

        VerificarProblemaDePredicadoSemDiscriminador(body);
    }

    /// <summary>
    /// Os dois erros de forma vizinhos, que antes divergiam: predicado vazio escapava como 500 e
    /// discriminador desconhecido já respondia 400. A recusa é a mesma para os dois.
    /// </summary>
    [Theory(DisplayName = "POST com predicado inválido devolve 400 uniformemente")]
    [InlineData("""{}""")]
    [InlineData("""{"$tipo":"inexistente"}""")]
    public async Task Criar_PredicadoInvalido_Retorna400(string predicadoJson)
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        HttpResponseMessage response = await PostAsync(
            client,
            "/api/selecao/admin/obrigatoriedades-legais",
            PayloadCom(JsonSerializer.Deserialize<JsonElement>(predicadoJson)));

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"body: {body}");
    }

    /// <summary>O mesmo corpo com o discriminador no lugar continua sendo aceito.</summary>
    [Fact(DisplayName = "POST com predicado e '$tipo' declarado continua criando a regra")]
    public async Task Criar_PredicadoComDiscriminador_Cria()
    {
        using HttpClient client = await ClienteComConcessaoAsync();

        HttpResponseMessage response = await PostAsync(
            client,
            "/api/selecao/admin/obrigatoriedades-legais",
            PayloadCom(PredicadoConcorrenciaDupla));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"body: {await response.Content.ReadAsStringAsync()}");
    }

    private static Dictionary<string, object> PredicadoSemDiscriminador(string chaveErrada) => new(StringComparer.Ordinal)
    {
        [chaveErrada] = "concorrenciaDuplaObrigatoria",
    };

    private static void VerificarProblemaDePredicadoSemDiscriminador(string body)
    {
        using JsonDocument problema = JsonDocument.Parse(body);
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.requisicao.malformada");
        string detalhe = problema.RootElement.GetProperty("detail").GetString()!;
        detalhe.Should().Contain("'predicado'").And.Contain("'$tipo'");
        detalhe.Should().NotContain("Unifesspa.UniPlus",
            "o nome do tipo CLR que falhou a desserialização não é assunto de quem chamou");
    }

    /// <summary>O corpo mínimo aceito pelo endpoint, variando só o predicado.</summary>
    private static object PayloadCom(object predicado) => new
    {
        tipoProcessoCodigo = "*",
        categoria = "OUTROS",
        regraCodigo = UniqueRegraCodigo(),
        predicado,
        descricaoHumana = "Predicado sob teste de leitura do corpo",
        baseLegal = "Lei 12.711/2012 art.1º",
        vigenciaInicio = "2026-01-01",
        vigenciaFim = (string?)null,
        atoNormativoUrl = (string?)null,
        portariaInternaCodigo = (string?)null,
    };
}
