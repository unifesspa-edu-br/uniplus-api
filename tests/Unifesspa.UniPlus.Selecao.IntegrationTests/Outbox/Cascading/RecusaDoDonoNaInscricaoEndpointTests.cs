namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Quem chega à inscrição de um processo pelo candidato, em cada operação que lê ou muda a inscrição do dono do token:
/// a de outro CPF responde como inexistente, a mesma pessoa com outra conta é identidade trocada, e o claim de CPF que
/// não é um CPF recusa sem ecoar o valor. A resposta nunca traz o CPF nem o nome.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RecusaDoDonoNaInscricaoEndpointTests
{
    private const string Dono = "conta-fernanda-drumond";
    private const string ClaimDeCpfInvalido = "cpf-invalido-xyz";
    private const string OutraConta = "conta-fernanda-drumond-trocada";

    private readonly CascadingFixture _fixture;

    public RecusaDoDonoNaInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public static TheoryData<string> Operacoes { get; } = new()
    {
        "cancelar", "descartar", "avaliar", "obter", "formulario", "submeter",
    };

    [Theory(DisplayName = "A inscrição de outro CPF responde 404 inscrição não encontrada, sem CPF nem nome")]
    [MemberData(nameof(Operacoes))]
    public async Task InscricaoDeOutroCpf_Responde404(string operacao)
    {
        Guid processoId = await SemearRascunhoDaFernandaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(operacao, processoId, PersonasDeTeste.KevinCarvalho.Valor, "conta-kevin-carvalho");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.inscricao.nao_encontrada");
        SemDadoPessoal(corpo);
    }

    [Theory(DisplayName = "O mesmo CPF com outra conta responde 403 dono divergente, sem CPF nem nome")]
    [MemberData(nameof(Operacoes))]
    public async Task MesmoCpfComOutraConta_Responde403(string operacao)
    {
        Guid processoId = await SemearRascunhoDaFernandaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(operacao, processoId, PersonasDeTeste.FernandaDrumond.Valor, OutraConta);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.inscricao.dono_divergente");
        SemDadoPessoal(corpo);
    }

    [Theory(DisplayName = "O claim de CPF que não é um CPF responde 403 cpf ausente no token, sem ecoar o valor")]
    [MemberData(nameof(Operacoes))]
    public async Task ClaimDeCpfInvalido_Responde403(string operacao)
    {
        Guid processoId = await SemearRascunhoDaFernandaAsync();

        using HttpResponseMessage resposta = await EnviarAsync(operacao, processoId, ClaimDeCpfInvalido, Dono);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.selecao.inscricao.cpf_ausente_no_token").And.NotContain(ClaimDeCpfInvalido);
        SemDadoPessoal(corpo);
    }

    [Theory(DisplayName = "Sem inscrição no processo, a resposta é 404 inscrição não encontrada")]
    [MemberData(nameof(Operacoes))]
    public async Task SemInscricao_Responde404(string operacao)
    {
        using HttpResponseMessage resposta = await EnviarAsync(operacao, Guid.CreateVersion7(), PersonasDeTeste.FernandaDrumond.Valor, Dono);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.selecao.inscricao.nao_encontrada");
    }

    private static void SemDadoPessoal(string corpo)
    {
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor).And.NotContain(PersonasDeTeste.KevinCarvalho.Valor);
        corpo.Should().NotContain("Fernanda").And.NotContain("Kevin");
    }

    private async Task<Guid> SemearRascunhoDaFernandaAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Dono da inscrição {Guid.CreateVersion7()}" });
        Inscricao rascunho = Inscricao.Iniciar(processo.Id, PersonasDeTeste.FernandaDrumond, Dono, TestAuthHandler.DefaultIssuer, Guid.CreateVersion7()).Value!;
        await escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(rascunho);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();
        return processo.Id;
    }

    private static (HttpMethod Metodo, string Rota, string Media, string? Corpo) PedidoDe(string operacao, Guid processoId) => operacao switch
    {
        "cancelar" => (HttpMethod.Post, $"{RaizDaInscricao(processoId)}/cancelamento", "application/json", "{}"),
        "descartar" => (HttpMethod.Delete, RaizDaInscricao(processoId), "application/json", null),
        "avaliar" => (HttpMethod.Post, $"{RaizDaInscricao(processoId)}/avaliacoes", "application/vnd.uniplus.avaliacao-da-inscricao.v1+json", "{}"),
        "obter" => (HttpMethod.Get, RaizDaInscricao(processoId), "application/vnd.uniplus.inscricao.v1+json", null),
        "formulario" => (HttpMethod.Get, $"{RaizDaInscricao(processoId)}/formulario", "application/vnd.uniplus.formulario-inscricao.v1+json", null),
        "submeter" => (HttpMethod.Post, $"{RaizDaInscricao(processoId)}/submissao", "application/vnd.uniplus.recibo-da-inscricao.v1+json",
            JsonSerializer.Serialize(new { ofertaCursoId = Guid.CreateVersion7(), respostas = new { } })),
        _ => throw new ArgumentOutOfRangeException(nameof(operacao), operacao, "Operação desconhecida."),
    };

    private static string RaizDaInscricao(Guid processoId) => $"/api/selecao/processos-seletivos/{processoId}/inscricao";

    private async Task<HttpResponseMessage> EnviarAsync(string operacao, Guid processoId, string cpfDoToken, string contaDoToken)
    {
        (HttpMethod metodo, string rota, string media, string? corpo) = PedidoDe(operacao, processoId);
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Content = corpo is null ? null : new StringContent(corpo, Encoding.UTF8, "application/json");
        ComCabecalhos(request, cpfDoToken, contaDoToken, media);
        using HttpClient client = _fixture.Factory.CreateClient();
        return await client.SendAsync(request);
    }

    private static void ComCabecalhos(HttpRequestMessage request, string cpfDoToken, string contaDoToken, string media)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, contaDoToken);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, cpfDoToken);
        request.Headers.TryAddWithoutValidation("Accept", media);
        request.Headers.TryAddWithoutValidation("If-Match", "\"qualquer\"");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
    }
}
