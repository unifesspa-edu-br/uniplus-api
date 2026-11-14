namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A ação interna da fase decidida na unidade responsável pela fase, com o ponto de decisão, a política de registro e a
/// leitura da versão publicada reais do host: a concessão só na unidade dona não basta, a inscrição no processo impede, a
/// negativa é registrada, o registro indisponível responde 503 e a consulta de capacidade não registra.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AcessoAFaseDoProcessoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Betina = "betina.neves";
    private const string Caroline = "caroline.lima";

    private readonly CascadingFixture _fixture;

    public AcessoAFaseDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Betina, Caroline })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A concessão só na unidade dona do processo é recusada; a da unidade da fase decide a ação")]
    public async Task Acao_DecideNaUnidadeDaFase_NaoNaDona()
    {
        Publicado publicado = await PublicarAsync(nameof(Acao_DecideNaUnidadeDaFase_NaoNaDona));
        await ConcederAsync(Leandro, publicado.UnidadeDona);
        await ConcederAsync(Betina, UnidadesDeTeste.Cga.OrigemId);

        string naDona = await ResultadoAsync(Leandro, publicado.Rota("acao"));
        string naFase = await ResultadoAsync(Betina, publicado.Rota("acao"));

        naDona.Should().Be(nameof(ResultadoDoAcesso.Negado));
        naFase.Should().Be(nameof(ResultadoDoAcesso.Permitido));
    }

    [Fact(DisplayName = "Quem tem a permissão na unidade da fase e está inscrito no processo é impedido")]
    public async Task Acao_InscritoNoProcesso_Impedido()
    {
        Publicado publicado = await PublicarAsync(nameof(Acao_InscritoNoProcesso_Impedido));
        await ConcederAsync(Caroline, UnidadesDeTeste.Cga.OrigemId);
        await InscreverAsync(publicado, Caroline);

        string resultado = await ResultadoAsync(Caroline, publicado.Rota("acao"));

        resultado.Should().Be(nameof(ResultadoDoAcesso.Impedido));
    }

    [Fact(DisplayName = "A negativa da ação da fase é registrada com o processo e a fase; a permissão de dado interno permitida não")]
    public async Task Acao_Negativa_Registrada()
    {
        Publicado publicado = await PublicarAsync(nameof(Acao_Negativa_Registrada));
        await ConcederAsync(Leandro, publicado.UnidadeDona);
        await ConcederAsync(Betina, UnidadesDeTeste.Cga.OrigemId);

        await ResultadoAsync(Leandro, publicado.Rota("acao"));
        await ResultadoAsync(Betina, publicado.Rota("acao"));

        RegistroDecisaoAcesso registro = Registros().Where(r => r.ProcessoId == publicado.ProcessoId)
            .Should().ContainSingle("só a negativa é gravada").Subject;
        registro.Permitido.Should().BeFalse();
        registro.AtorSubject.Should().Be(Leandro);
        registro.Permissao.Should().Be(PermissaoSelecao.DocumentosDaFasePublicar.Codigo);
        registro.ResourceId.Should().Be(publicado.FaseId);
    }

    [Fact(DisplayName = "A fase que não se resolve é negada sem registrar decisão")]
    public async Task Acao_FaseNaoResolvida_NegaSemRegistro()
    {
        Publicado publicado = await PublicarAsync(nameof(Acao_FaseNaoResolvida_NegaSemRegistro));
        Publicado outro = await PublicarAsync(nameof(Acao_FaseNaoResolvida_NegaSemRegistro) + "-outro");
        await ConcederAsync(Betina, UnidadesDeTeste.Cga.OrigemId);

        string inexistente = await ResultadoAsync(Betina, (publicado with { FaseId = Guid.CreateVersion7() }).Rota("acao"));
        string deOutroProcesso = await ResultadoAsync(Betina, (publicado with { FaseId = outro.FaseId }).Rota("acao"));

        inexistente.Should().Be(nameof(ResultadoDoAcesso.Negado));
        deOutroProcesso.Should().Be(nameof(ResultadoDoAcesso.Negado));
        Registros().Where(r => r.ProcessoId == publicado.ProcessoId).Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o registro de acesso indisponível, a recusa da ação da fase responde 503")]
    public async Task Acao_RegistroIndisponivel_503()
    {
        Publicado publicado = await PublicarAsync(nameof(Acao_RegistroIndisponivel_503));
        await ConcederAsync(Leandro, publicado.UnidadeDona);

        using HttpResponseMessage resposta = await GetComRegistroIndisponivelAsync(Leandro, publicado.Rota("acao"));

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "A consulta de capacidade da ação da fase usa a unidade da fase e não registra decisão")]
    public async Task Capacidade_NaoRegistra()
    {
        Publicado publicado = await PublicarAsync(nameof(Capacidade_NaoRegistra));
        await ConcederAsync(Leandro, publicado.UnidadeDona);
        await ConcederAsync(Betina, UnidadesDeTeste.Cga.OrigemId);

        string naDona = await ResultadoAsync(Leandro, publicado.Rota("capacidade"));
        string naFase = await ResultadoAsync(Betina, publicado.Rota("capacidade"));

        naDona.Should().Be(nameof(ResultadoDoAcesso.Negado));
        naFase.Should().Be(nameof(ResultadoDoAcesso.Permitido));
        Registros().Where(r => r.ProcessoId == publicado.ProcessoId).Should().BeEmpty();
    }

    private IReadOnlyList<RegistroDecisaoAcesso> Registros() =>
        _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados;

    private sealed record Publicado(Guid ProcessoId, Guid UnidadeDona, Guid FaseId, Guid VersaoId)
    {
        public string Rota(string acao) => $"/api/selecao/_teste/processos-seletivos/{ProcessoId}/fases/{FaseId}/{acao}";
    }

    private async Task ConcederAsync(string sub, Guid unidade)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, PermissaoSelecao.DocumentosDaFasePublicar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    // O processo é publicado de verdade, com a fase única sob a unidade da fase, que não é a dona do processo.
    private async Task<Publicado> PublicarAsync(string nome)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoEdital, Guid faseId, Guid unidadeDona) = await SemearRascunhoAsync(nome);

        using (HttpResponseMessage publicacao = await SendAdminAsync($"/api/selecao/processos-seletivos/{processoId}/publicacao", CorpoDoAto(documentoEdital)))
        {
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }

        return new Publicado(processoId, unidadeDona, faseId, await VersaoVigenteAsync(processoId));
    }

    private async Task<(Guid ProcessoId, Guid DocumentoEdital, Guid FaseId, Guid UnidadeDona)> SemearRascunhoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}", unidadeResponsavel: UnidadesDeTeste.Cga);

        return (processo.Id, documento.Id, processo.CronogramaFases.Single().Id, processo.UnidadeAdministradoraOrigemId);
    }

    private async Task<Guid> VersaoVigenteAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UniPlusDbContext>().VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).OrderByDescending(v => v.NumeroVersao).Select(v => v.Id).FirstAsync();
    }

    private async Task InscreverAsync(Publicado publicado, string sub)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        Cpf cpf = Cpf.Criar(PersonasDeTeste.Candidatas.First(static c => c.Nome.StartsWith("Helena", StringComparison.Ordinal)).Cpf).Value!;
        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), publicado.ProcessoId, cpf, sub, TestAuthHandler.DefaultIssuer, SituacaoInscricao.Submetida, publicado.VersaoId, null, null,
            "000002", null, null, null, new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), revisao: 1, DateTimeOffset.UtcNow, null);
        await scope.ServiceProvider.GetRequiredService<IInscricaoRepository>().AdicionarAsync(inscricao);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();
    }

    private async Task<string> ResultadoAsync(string sub, string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await client.SendAsync(Requisicao(sub, rota));
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);

        return json.RootElement.GetProperty("resultado").GetString()!;
    }

    private async Task<HttpResponseMessage> GetComRegistroIndisponivelAsync(string sub, string rota)
    {
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient client = comFalha.CreateClient();

        return await client.SendAsync(Requisicao(sub, rota));
    }

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker fora"));
    }

    private static HttpRequestMessage Requisicao(string sub, string rota)
    {
        HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);

        return request;
    }

    private async Task<HttpResponseMessage> SendAdminAsync(string rota, object corpo)
    {
        HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        return await client.SendAsync(request);
    }

    private static object CorpoDoAto(Guid documentoId) => new
    {
        numero = "001/2026",
        documentoEditalId = documentoId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
    };
}
