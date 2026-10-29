namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O servidor com permissão sobre o processo fica impedido de operá-lo quando tem nele uma inscrição enviada e não
/// cancelada (Lei nº 9.784/1999, art. 18, I); nos demais processos e como candidata, segue como qualquer pessoa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ImpedimentoPorInscricaoEndpointTests : IAsyncLifetime
{
    private const string Betina = "betina.neves";
    private const string CodigoDoImpedimento = "uniplus.auth.impedido_por_inscricao_no_processo";

    private readonly CascadingFixture _fixture;

    public ImpedimentoPorInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Betina);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Sem inscrição, com rascunho ou com a inscrição cancelada, a servidora opera o processo")]
    public async Task SemInscricaoEnviada_Opera()
    {
        Guid semInscricao = await SemearProcessoAsync();
        Guid comRascunho = await SemearProcessoAsync(SituacaoInscricao.Rascunho);
        Guid cancelada = await SemearProcessoAsync(SituacaoInscricao.Cancelada);
        await ConcederAsync(semInscricao, comRascunho, cancelada);

        using HttpResponseMessage a = await PutTaxaAsync(semInscricao);
        using HttpResponseMessage b = await PutTaxaAsync(comRascunho);
        using HttpResponseMessage c = await PutTaxaAsync(cancelada);

        a.StatusCode.Should().Be(HttpStatusCode.NoContent);
        b.StatusCode.Should().Be(HttpStatusCode.NoContent);
        c.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Com a inscrição enviada, toda operação do processo responde 403 impedido e o registro traz o motivo")]
    public async Task InscricaoEnviada_Impede()
    {
        Guid processo = await SemearProcessoAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(processo);

        using HttpResponseMessage configurar = await PutTaxaAsync(processo);
        using HttpResponseMessage inscricoes = await SendAsync(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processo}/inscricoes");

        configurar.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        inscricoes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await configurar.Content.ReadAsStringAsync()).Should().Contain(CodigoDoImpedimento);
        (await inscricoes.Content.ReadAsStringAsync()).Should().Contain(CodigoDoImpedimento);
        _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == processo)
            .Should().NotBeEmpty().And.OnlyContain(r => !r.Permitido && r.MotivoNegativa == "impedido_por_inscricao_no_processo");
    }

    [Fact(DisplayName = "O impedimento vale só no processo em que está inscrita")]
    public async Task InscricaoEmOutroProcesso_NaoImpede()
    {
        Guid inscrita = await SemearProcessoAsync(SituacaoInscricao.Submetida);
        Guid outro = await SemearProcessoAsync();
        await ConcederAsync(inscrita, outro);

        using HttpResponseMessage naInscrita = await PutTaxaAsync(inscrita);
        using HttpResponseMessage noOutro = await PutTaxaAsync(outro);

        naInscrita.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noOutro.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "Inscrição de outro emissor com o mesmo sub não impede: a identidade é o par emissor e sub")]
    public async Task InscricaoDeOutroEmissorComOMesmoSub_NaoImpede()
    {
        Guid processo = await SemearProcessoAsync(SituacaoInscricao.Submetida, emissor: "https://outro-emissor.exemplo/realms/outro");
        await ConcederAsync(processo);

        using HttpResponseMessage resposta = await PutTaxaAsync(processo);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A concessão nova, aprovada depois da inscrição, não supera o impedimento")]
    public async Task ConcessaoNovaDepoisDaInscricao_NaoSupera()
    {
        Guid processo = await SemearProcessoAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(processo);
        Guid unidade = await UnidadeDoProcessoAsync(processo);
        Guid outroPerfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo, PermissaoSelecao.ProcessosAcompanhar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Betina, outroPerfil, EscopoDaConcessao.DaUnidade(unidade));

        using HttpResponseMessage resposta = await PutTaxaAsync(processo);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Impedida no processo, a servidora segue consultando as próprias inscrições como candidata")]
    public async Task ComoCandidata_SegueConsultando()
    {
        Guid processo = await SemearProcessoAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(processo);

        using HttpResponseMessage minhas = await SendAsync(
            HttpMethod.Get, "/api/selecao/minhas-inscricoes", candidata: true);

        minhas.StatusCode.Should().Be(HttpStatusCode.OK, await minhas.Content.ReadAsStringAsync());
    }

    private async Task ConcederAsync(params Guid[] processos)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo, PermissaoSelecao.InscricoesConsultar.Codigo);
        foreach (Guid processo in processos)
        {
            await ConcessoesDeTeste.ConcederAsync(
                _fixture.Factory.Services, Betina, perfil, EscopoDaConcessao.DaUnidade(await UnidadeDoProcessoAsync(processo)));
        }
    }

    private async Task<Guid> SemearProcessoAsync(SituacaoInscricao? inscricaoDeBetina = null, string? emissor = null)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Impedimento {Guid.CreateVersion7()}");

        if (inscricaoDeBetina is { } situacao)
        {
            bool cancelada = situacao == SituacaoInscricao.Cancelada;
            Inscricao inscricao = Inscricao.Reidratar(
                Guid.CreateVersion7(), processo.Id, PersonasDeTeste.FernandaDrumond, Betina, emissor ?? TestAuthHandler.DefaultIssuer, situacao,
                Guid.CreateVersion7(), null, null,
                situacao == SituacaoInscricao.Rascunho ? null : "000001",
                cancelada ? DateTimeOffset.UtcNow : null, cancelada ? Betina : null, cancelada ? "a pedido do candidato" : null,
                new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
                [], new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
                revisao: 1, DateTimeOffset.UtcNow, null);
            await scope.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(inscricao);
            await scope.ServiceProvider.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();
        }

        return processo.Id;
    }

    private async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    private Task<HttpResponseMessage> PutTaxaAsync(Guid processoId) =>
        SendAsync(HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/taxa-inscricao",
            new { cobra = false, valor = (decimal?)null, fundamentos = (string[]?)null });

    private async Task<HttpResponseMessage> SendAsync(HttpMethod metodo, string rota, object? corpo = null, bool candidata = false)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Betina);
        if (candidata)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "candidato");
            request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, PersonasDeTeste.FernandaDrumond.Valor);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        }

        return await client.SendAsync(request);
    }
}
