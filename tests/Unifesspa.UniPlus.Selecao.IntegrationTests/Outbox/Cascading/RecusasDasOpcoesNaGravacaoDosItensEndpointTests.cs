namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A gravação dos itens da inscrição pelo HTTP devolve a recusa de cada grupo de opções e de cada limite de data
/// inválido, cada uma no caminho do seu elemento dentro da restrição.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RecusasDasOpcoesNaGravacaoDosItensEndpointTests
{
    private readonly CascadingFixture _fixture;

    public RecusasDasOpcoesNaGravacaoDosItensEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Opções com um grupo nulo e outro com condição inválida recusam os dois grupos, cada um no seu caminho")]
    public async Task DefinirItens_OpcoesComGrupoNuloEGrupoRecusado_RecusaOsDoisGrupos()
    {
        IReadOnlyList<(string? Campo, string? Codigo)> recusas = await RecusasAsync(OpcoesComGrupoNuloEGrupoRecusado());

        recusas.Should().HaveCount(2);
        recusas[0].Campo.Should().EndWith("restricoes[0].entradas[0]");
        recusas[0].Codigo.Should().Be("uniplus.restricao_valor.opcoes_vazias");
        recusas[1].Campo.Should().EndWith("restricoes[0].entradas[1].quando[0][0].operador");
        recusas[1].Codigo.Should().Be("uniplus.condicao_dnf.operador_invalido");
    }

    [Fact(DisplayName = "Faixa de data com os dois limites inválidos recusa o mínimo e depois o máximo")]
    public async Task DefinirItens_FaixaDeDataComOsDoisLimitesInvalidos_RecusaOsDoisLimites()
    {
        object limiteInvalido = new { ancora = "HOJE" };
        object faixa = new { tipo = "FAIXA_DE_DATA", dataMinima = limiteInvalido, dataMaxima = limiteInvalido };

        IReadOnlyList<(string? Campo, string? Codigo)> recusas = await RecusasAsync(faixa);

        recusas.Should().HaveCount(2);
        recusas[0].Campo.Should().EndWith("restricoes[0].dataMinima");
        recusas[1].Campo.Should().EndWith("restricoes[0].dataMaxima");
        recusas.Should().OnlyContain(static recusa => recusa.Codigo == "uniplus.restricao_valor.limite_de_data_invalido");
    }

    private static object OpcoesComGrupoNuloEGrupoRecusado() => new
    {
        tipo = "OPCOES_PERMITIDAS",
        entradas = new object?[]
        {
            null,
            new { quando = new[] { new[] { new { fato = "QUILOMBOLA", operador = "OPERADOR_QUE_NAO_EXISTE", valor = true } } }, valores = new[] { "SIM" } },
        },
    };

    private async Task<IReadOnlyList<(string? Campo, string? Codigo)>> RecusasAsync(object restricao)
    {
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage recusado = await ProcessoSeletivoHttp.DefinirItensAsync(
            client, processoId, new { itens = new[] { ItemComRestricao(restricao) }, grupos = Array.Empty<object>() });

        string corpo = await recusado.Content.ReadAsStringAsync();
        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static object ItemComRestricao(object restricao) => new
    {
        fatoCodigo = "QUILOMBOLA",
        ordem = FormularioDeTeste.PrimeiraOrdemDeInscricao,
        paginaCodigo = FormularioDeTeste.Secao,
        apresentacao = new { rotulo = "Quilombola", tipoDeCampo = "SIM_OU_NAO" },
        regras = new { obrigatoriedade = new { tipo = "SEMPRE" }, restricoes = new[] { restricao } },
    };

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Recusas das opções {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
