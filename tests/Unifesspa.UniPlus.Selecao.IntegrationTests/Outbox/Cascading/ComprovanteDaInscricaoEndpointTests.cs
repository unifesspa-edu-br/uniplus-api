namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// <c>GET /inscricoes/{inscricaoId}/comprovantes/{numero}</c> pela borda HTTP: só o dono lê, o número que não existe e o
/// zero respondem 404, a inscrição cancelada responde 422 e o media type é negociado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ComprovanteDaInscricaoEndpointTests
{
    private const string Media = "application/vnd.uniplus.comprovante-inscricao.v1+json";
    private const string Dono = "sub-comprovante";

    private readonly CascadingFixture _fixture;

    public ComprovanteDaInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O número zero, o negativo e o que a inscrição não tem respondem 404 comprovante não encontrado")]
    public async Task NumeroForaDaFaixa_Responde404()
    {
        Guid inscricao = await SemearAsync(SituacaoInscricao.Submetida, comComprovante: true);

        foreach (int numero in new[] { 0, -1, 2 })
        {
            using HttpResponseMessage resposta = await GetAsync(inscricao, numero, PersonasDeTeste.FernandaDrumond, Media);

            resposta.StatusCode.Should().Be(HttpStatusCode.NotFound, $"o comprovante {numero} não existe");
            (await resposta.Content.ReadAsStringAsync()).Should().Contain("comprovante_nao_encontrado");
        }
    }

    [Fact(DisplayName = "A inscrição de outro candidato responde 404, como a inexistente")]
    public async Task InscricaoDeOutroCandidato_Responde404()
    {
        Guid inscricao = await SemearAsync(SituacaoInscricao.Submetida, comComprovante: true);

        using HttpResponseMessage deOutro = await GetAsync(inscricao, 1, PersonasDeTeste.KevinCarvalho, Media);
        using HttpResponseMessage inexistente = await GetAsync(Guid.CreateVersion7(), 1, PersonasDeTeste.FernandaDrumond, Media);

        deOutro.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A inscrição cancelada responde 422 ao dono e 404 a quem não é o dono")]
    public async Task InscricaoCancelada_Responde422AoDono()
    {
        Guid inscricao = await SemearAsync(SituacaoInscricao.Cancelada, comComprovante: true);

        using HttpResponseMessage doDono = await GetAsync(inscricao, 1, PersonasDeTeste.FernandaDrumond, Media);
        using HttpResponseMessage deOutro = await GetAsync(inscricao, 1, PersonasDeTeste.KevinCarvalho, Media);

        doDono.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await doDono.Content.ReadAsStringAsync()).Should().Contain("comprovante_de_inscricao_cancelada");
        deOutro.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Sem o media type do comprovante no Accept, responde 406")]
    public async Task SemOMediaType_Responde406()
    {
        Guid inscricao = await SemearAsync(SituacaoInscricao.Submetida, comComprovante: true);

        using HttpResponseMessage resposta = await GetAsync(inscricao, 1, PersonasDeTeste.FernandaDrumond, "application/vnd.uniplus.outro-recurso.v1+json");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    [Fact(DisplayName = "A rota antiga do comprovante por processo deixou de existir")]
    public async Task RotaAntigaPorProcesso_NaoExiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/inscricao/comprovantes/1", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Dono);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, PersonasDeTeste.FernandaDrumond.Valor);
        request.Headers.TryAddWithoutValidation("Accept", Media);

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SemearAsync(SituacaoInscricao situacao, bool comComprovante)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Comprovante {Guid.CreateVersion7()}");
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        bool cancelada = situacao == SituacaoInscricao.Cancelada;
        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processo.Id, cpf, Dono, TestAuthHandler.DefaultIssuer, situacao, Guid.CreateVersion7(), null, null,
            "000001", cancelada ? DateTimeOffset.UtcNow : null, cancelada ? Dono : null, cancelada ? "a pedido do candidato" : null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CPF"] = JsonSerializer.SerializeToElement(cpf.Valor) },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
            {
                ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            },
            revisao: 1, DateTimeOffset.UtcNow, null);
        IInscricaoRepository repositorio = escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>();
        await repositorio.AdicionarAsync(inscricao);
        if (comComprovante)
        {
            await repositorio.AdicionarComprovanteAsync(ComprovanteInscricao.Emitir(
                inscricao.Id, "000001", DateTimeOffset.UtcNow, null,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], []));
        }

        await escopo.ServiceProvider.GetRequiredService<ISelecaoUnitOfWork>().SalvarAlteracoesAsync();

        return inscricao.Id;
    }

    private async Task<HttpResponseMessage> GetAsync(Guid inscricaoId, int numero, Cpf cpf, string accept)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"/api/selecao/inscricoes/{inscricaoId}/comprovantes/{numero}", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, cpf == PersonasDeTeste.FernandaDrumond ? Dono : "sub-outro");
        request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, cpf.Valor);
        request.Headers.TryAddWithoutValidation("Accept", accept);

        return await client.SendAsync(request);
    }
}
