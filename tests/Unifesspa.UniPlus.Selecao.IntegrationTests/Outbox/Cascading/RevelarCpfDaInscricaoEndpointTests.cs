namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Revelar o CPF completo é uma operação própria, de uma inscrição por vez: exige a permissão de revelar na unidade dona
/// do processo, grava a decisão com a inscrição e nunca altera o que a lista mostra.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RevelarCpfDaInscricaoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Betina = "betina.neves";
    private const string Caroline = "caroline.lima";

    private readonly CascadingFixture _fixture;

    public RevelarCpfDaInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina, Caroline })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Com a permissão de revelar na unidade dona, devolve o CPF completo sem cache e grava a decisão com a inscrição")]
    public async Task ComPermissao_RevelaEGravaADecisaoComAInscricao()
    {
        Cenario cenario = await SemearAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesRevelarCpf);

        using HttpResponseMessage resposta = await RevelarAsync(Leandro, cenario.ProcessoId, cenario.InscricaoId);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Headers.CacheControl!.NoStore.Should().BeTrue();
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("cpfCompleto").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Formatado);

        RegistroDecisaoAcesso decisao = Decisoes(cenario.ProcessoId, cenario.InscricaoId).Should().ContainSingle().Subject;
        decisao.Permitido.Should().BeTrue();
        decisao.Permissao.Should().Be(PermissaoSelecao.InscricoesRevelarCpf.Codigo);
        decisao.GrantId.Should().NotBeNull("a concessão usada identifica quem revelou");
        JsonSerializer.Serialize(decisao).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
    }

    [Fact(DisplayName = "Consultar as inscrições não basta: a revelação responde 404 e não grava decisão da inscrição")]
    public async Task SoConsultar_NaoRevela()
    {
        Cenario cenario = await SemearAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(Caroline, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage resposta = await RevelarAsync(Caroline, cenario.ProcessoId, cenario.InscricaoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
        Decisoes(cenario.ProcessoId, cenario.InscricaoId).Should().NotBeEmpty().And.OnlyContain(r => !r.Permitido);
    }

    [Fact(DisplayName = "A permissão em outra unidade, a inscrição de outro processo e o rascunho respondem o mesmo 404")]
    public async Task ForaDoAlcance_404()
    {
        Cenario cenario = await SemearAsync(SituacaoInscricao.Submetida);
        Cenario outroProcesso = await SemearAsync(SituacaoInscricao.Submetida);
        Cenario rascunho = await SemearAsync(SituacaoInscricao.Rascunho);
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesRevelarCpf);
        await ConcederAsync(Betina, Guid.CreateVersion7(), PermissaoSelecao.InscricoesRevelarCpf);
        await ConcederAsync(Caroline, rascunho.UnidadeId, PermissaoSelecao.InscricoesRevelarCpf);

        using HttpResponseMessage outraUnidade = await RevelarAsync(Betina, cenario.ProcessoId, cenario.InscricaoId);
        using HttpResponseMessage inscricaoDeOutroProcesso = await RevelarAsync(Leandro, cenario.ProcessoId, outroProcesso.InscricaoId);
        using HttpResponseMessage emRascunho = await RevelarAsync(Caroline, rascunho.ProcessoId, rascunho.InscricaoId);

        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inscricaoDeOutroProcesso.StatusCode.Should().Be(HttpStatusCode.NotFound);
        emRascunho.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Decisoes(cenario.ProcessoId, outroProcesso.InscricaoId).Should().BeEmpty("a trilha nunca nomeia inscrição de outro processo");
        Decisoes(rascunho.ProcessoId, rascunho.InscricaoId).Should().BeEmpty();
    }

    [Fact(DisplayName = "Quem pode revelar continua vendo o CPF mascarado na lista")]
    public async Task Lista_SegueMascarada()
    {
        Cenario cenario = await SemearAsync(SituacaoInscricao.Submetida);
        await ConcederAsync(Leandro, cenario.UnidadeId, PermissaoSelecao.InscricoesRevelarCpf, PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage lista = await SendAsync(
            Leandro, HttpMethod.Get, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes");

        string corpo = await lista.Content.ReadAsStringAsync();
        lista.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        corpo.Should().Contain(PersonasDeTeste.FernandaDrumond.Mascarado)
            .And.NotContain(PersonasDeTeste.FernandaDrumond.Valor)
            .And.NotContain(PersonasDeTeste.FernandaDrumond.Formatado);
    }

    [Fact(DisplayName = "A servidora inscrita no processo, ainda que com a permissão, responde 403 impedida")]
    public async Task ServidoraInscrita_Impedida()
    {
        Cenario cenario = await SemearAsync(SituacaoInscricao.Submetida, donaDaInscricao: Betina);
        await ConcederAsync(Betina, cenario.UnidadeId, PermissaoSelecao.InscricoesRevelarCpf);

        using HttpResponseMessage resposta = await RevelarAsync(Betina, cenario.ProcessoId, cenario.InscricaoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private IReadOnlyList<RegistroDecisaoAcesso> Decisoes(Guid processoId, Guid inscricaoId) =>
        [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == processoId
                && r.Permissao == PermissaoSelecao.InscricoesRevelarCpf.Codigo
                && r.RecursoId == inscricaoId)];

    private async Task ConcederAsync(string sub, Guid unidade, params PermissaoSelecao[] permissoes)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, [.. permissoes.Select(static p => p.Codigo)]);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<Cenario> SemearAsync(SituacaoInscricao situacao, string donaDaInscricao = "sub-fernanda")
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Revelar CPF {Guid.CreateVersion7()}");

        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processo.Id, PersonasDeTeste.FernandaDrumond, donaDaInscricao, TestAuthHandler.DefaultIssuer, situacao,
            Guid.CreateVersion7(), null, null,
            situacao == SituacaoInscricao.Rascunho ? null : "000001",
            null, null, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal),
            [], new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            revisao: 1, DateTimeOffset.UtcNow, null);
        await scope.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(inscricao);
        await scope.ServiceProvider.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        Guid unidade = await db.ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processo.Id).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
        return new Cenario(processo.Id, unidade, inscricao.Id);
    }

    private Task<HttpResponseMessage> RevelarAsync(string sub, Guid processoId, Guid inscricaoId) =>
        SendAsync(sub, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/inscricoes/{inscricaoId}/cpf/revelacao");

    private async Task<HttpResponseMessage> SendAsync(string sub, HttpMethod metodo, string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        return await client.SendAsync(request);
    }

    private sealed record Cenario(Guid ProcessoId, Guid UnidadeId, Guid InscricaoId);
}
