namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/classificacao</c> quando o pedido tem mais de uma recusa: elas saem
/// juntas na mesma resposta (campo antes da consulta ao catálogo), a raiz repete a primeira, nada é gravado e nenhuma
/// mensagem devolve o código ou a versão da regra que o operador enviou.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirClassificacaoRecusasSomadasEndpointTests
{
    private const string CodigoForaDoCatalogo = "REGRA_FORA_DO_CATALOGO";
    private const string VersaoForaDoCatalogo = "v77";
    private const string RegraNaoEncontrada = "uniplus.selecao.configuracao_classificacao.regra_nao_encontrada";
    private const string EscalaInvalida = "uniplus.selecao.configuracao_classificacao.escala_nota_final_invalida";
    private const string CasasObrigatorias = "uniplus.selecao.configuracao_classificacao.casas_arredondamento_obrigatorio";

    private readonly CascadingFixture _fixture;

    public DefinirClassificacaoRecusasSomadasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Regra de cálculo fora do catálogo e escala zero saem juntas: primeiro o campo, depois a regra")]
    public async Task CalculoForaDoCatalogoComEscalaZero_422ComAsDuasCausasNaOrdem()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(CalculoForaDoCatalogoComEscalaZero_422ComAsDuasCausasNaOrdem));

        using HttpResponseMessage resposta = await ctx.PutAsync(Corpo(
            calculo: CodigoForaDoCatalogo, arredondamento: null, casas: null, ordem: OrdemExistente, escala: 0m));

        await ConferirCausasAsync(resposta, ("escalaNotaFinal", EscalaInvalida), ("regraCalculoCodigo", RegraNaoEncontrada));
    }

    [Fact(DisplayName = "Casas de arredondamento ausentes e ordem de alocação fora do catálogo saem juntas")]
    public async Task CasasAusentesComOrdemForaDoCatalogo_422ComAsDuasCausasNaOrdem()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(CasasAusentesComOrdemForaDoCatalogo_422ComAsDuasCausasNaOrdem));

        using HttpResponseMessage resposta = await ctx.PutAsync(Corpo(
            calculo: CalculoLocal, arredondamento: ArredondamentoExistente, casas: null, ordem: CodigoForaDoCatalogo, escala: null));

        await ConferirCausasAsync(resposta, ("casasArredondamento", CasasObrigatorias), ("regraOrdemAlocacaoCodigo", RegraNaoEncontrada));
    }

    [Fact(DisplayName = "Só a regra fora do catálogo gera uma recusa em errors[], com o campo que a cita")]
    public async Task SoRegraForaDoCatalogo_422ComUmaCausaCujoCampoCitaARegra()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SoRegraForaDoCatalogo_422ComUmaCausaCujoCampoCitaARegra));

        using HttpResponseMessage resposta = await ctx.PutAsync(Corpo(
            calculo: CalculoLocal, arredondamento: ArredondamentoExistente, casas: 2, ordem: CodigoForaDoCatalogo, escala: null));

        await ConferirCausasAsync(resposta, ("regraOrdemAlocacaoCodigo", RegraNaoEncontrada));
    }

    [Fact(DisplayName = "Parâmetros recusados no item 0 e falta em dia de prova repetida no item 2 saem juntos, cada um no seu item")]
    public async Task EliminacaoComParametrosRecusadosEOutraRepetida_422ComOsDoisItens()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(EliminacaoComParametrosRecusadosEOutraRepetida_422ComOsDoisItens));
        using HttpResponseMessage resposta = await ctx.PutAsync(Corpo(
            calculo: CalculoLocal, arredondamento: ArredondamentoExistente, casas: 2, ordem: OrdemExistente, escala: null, FaltaComEtapaERepetida()));

        (await CamposDosItensDeEliminacaoAsync(resposta)).Should().Equal("regrasEliminacao[2]", "regrasEliminacao[0]");
    }

    [Fact(DisplayName = "Depois de recusas somadas, a classificação do processo continua a que já estava")]
    public async Task AposRecusasSomadas_NadaEGravado()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(AposRecusasSomadas_NadaEGravado));
        SituacaoDaClassificacao antes = await SituacaoPersistidaAsync(ctx);

        using HttpResponseMessage duasCausas = await ctx.PutAsync(Corpo(
            calculo: CodigoForaDoCatalogo, arredondamento: null, casas: null, ordem: OrdemExistente, escala: 0m));
        using HttpResponseMessage umaCausa = await ctx.PutAsync(Corpo(
            calculo: CalculoLocal, arredondamento: ArredondamentoExistente, casas: 3, ordem: CodigoForaDoCatalogo, escala: 500m));

        duasCausas.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        umaCausa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await SituacaoPersistidaAsync(ctx)).Should().Be(antes);
    }

    [Fact(DisplayName = "Nenhuma mensagem da recusa devolve o código nem a versão da regra enviados")]
    public async Task Recusa_NaoRepeteCodigoNemVersaoEnviados()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(Recusa_NaoRepeteCodigoNemVersaoEnviados));

        using HttpResponseMessage resposta = await ctx.PutAsync(Corpo(
            calculo: CodigoForaDoCatalogo, arredondamento: null, casas: null, ordem: CodigoForaDoCatalogo, escala: 0m));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        string[] textos = [.. Textos(problema.RootElement)];
        textos.Should().NotContain(texto => texto.Contains(CodigoForaDoCatalogo, StringComparison.OrdinalIgnoreCase));
        textos.Should().NotContain(texto => texto.Contains(VersaoForaDoCatalogo, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Quem não tem a permissão de configurar o processo recebe a recusa de acesso, sem nenhuma conferência do pedido")]
    public async Task SemPermissaoDeConfigurar_RecusaAntesDeConferirOPedido()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemPermissaoDeConfigurar_RecusaAntesDeConferirOPedido));
        string persona = $"pessoa.sem.permissao.{Guid.CreateVersion7():N}";
        await ConcederOutraPermissaoAsync(ctx, persona);
        SituacaoDaClassificacao antes = await SituacaoPersistidaAsync(ctx);

        using HttpResponseMessage resposta = await ctx.PutAsync(
            Corpo(calculo: CodigoForaDoCatalogo, arredondamento: null, casas: null, ordem: OrdemExistente, escala: 0m), persona);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, "o processo não é revelado a quem não pode configurá-lo");
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain(RegraNaoEncontrada).And.NotContain(EscalaInvalida);
        (await SituacaoPersistidaAsync(ctx)).Should().Be(antes);
    }

    private static string CalculoLocal => Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.CodigoDeContratoExtensions.Codigo(RegraDeCalculoDaNota.MediaPonderada);

    private static string ArredondamentoExistente => Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.CodigoDeContratoExtensions.Codigo(RegraDeArredondamentoDaNota.Truncar);

    private static string OrdemExistente => Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.CodigoDeContratoExtensions.Codigo(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria);

    private static object[] FaltaComEtapaERepetida()
    {
        string falta = Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato.CodigoDeContratoExtensions.Codigo(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem);
        return
        [
            new { regraCodigo = falta, regraVersao = "v1", etapaRef = Guid.CreateVersion7() },
            new { regraCodigo = falta, regraVersao = "v1" },
            new { regraCodigo = falta, regraVersao = "v1" },
        ];
    }

    private static async Task<string?[]> CamposDosItensDeEliminacaoAsync(HttpResponseMessage resposta)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => e.GetProperty("field").GetString())
            .Where(static campo => campo is not null && campo.StartsWith("regrasEliminacao[", StringComparison.Ordinal))];
    }

    private static async Task ConferirCausasAsync(HttpResponseMessage resposta, params (string Campo, string Codigo)[] causas)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];

        erros.Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Equal(causas.Select(static c => ((string?)c.Campo, (string?)c.Codigo)));
        problema.RootElement.GetProperty("code").GetString().Should().Be(erros[0].GetProperty("code").GetString());
        problema.RootElement.GetProperty("detail").GetString().Should().Be(erros[0].GetProperty("message").GetString());
        problema.RootElement.GetProperty("status").GetInt32().Should().Be((int)HttpStatusCode.UnprocessableEntity);
    }

    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.String => [elemento.GetString()!],
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(propriedade => Textos(propriedade.Value)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        _ => [],
    };

    // Só os códigos do catálogo de teste têm versão v1; o código fora do catálogo leva uma versão própria,
    // para a prova de que a mensagem não devolve nenhum dos dois.
    private static string VersaoDe(string codigo) => codigo == CodigoForaDoCatalogo ? VersaoForaDoCatalogo : "v1";

    private static object Corpo(string calculo, string? arredondamento, int? casas, string ordem, decimal? escala, object[]? eliminacoes = null) => new
    {
        regraCalculoCodigo = calculo,
        regraCalculoVersao = VersaoDe(calculo),
        regraArredondamentoCodigo = arredondamento,
        regraArredondamentoVersao = arredondamento is null ? null : VersaoDe(arredondamento),
        casasArredondamento = casas,
        regraOrdemAlocacaoCodigo = ordem,
        regraOrdemAlocacaoVersao = VersaoDe(ordem),
        regrasEliminacao = eliminacoes ?? [],
        baseadoEmEnem = false,
        escalaNotaFinal = escala,
    };

    private static async Task<SituacaoDaClassificacao> SituacaoPersistidaAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ConfiguracaoClassificacao c = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(x => x.ProcessoSeletivoId == ctx.ProcessoId);
        return new SituacaoDaClassificacao(
            c.RegraCalculo.Codigo, c.RegraArredondamento?.Codigo, c.CasasArredondamento, c.RegraOrdemAlocacao.Codigo, c.EscalaNotaFinal);
    }

    private static async Task ConcederOutraPermissaoAsync(Contexto ctx, string persona)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid unidade = await db.ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == ctx.ProcessoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(ctx.Api.Services, PermissaoSelecao.InscricoesConsultar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(ctx.Api.Services, persona, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });

        return new Contexto(api, api.CreateClient(), processo.Id);
    }

    private sealed record SituacaoDaClassificacao(
        string Calculo, string? Arredondamento, int? Casas, string OrdemDeAlocacao, decimal? Escala);

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutAsync(object corpo, string? persona = null)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/classificacao", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request, persona);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        private static void Autenticar(HttpRequestMessage request, string? persona)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            if (persona is null)
            {
                request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
                return;
            }

            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, persona);
        }
    }
}
