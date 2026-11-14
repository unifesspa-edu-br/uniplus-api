namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// <c>GET /processos-seletivos/{id}/inscricoes/{inscricaoId}/comprovantes/{numero}</c> pela borda HTTP: a unidade que consulta as
/// inscrições e tem a permissão de emitir emite o comprovante de qualquer inscrição do processo, a cancelada inclusive, que sai
/// identificada como cancelada. Sem a permissão de emitir a emissão é recusada, e o titular segue sem comprovante da cancelada.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class EmissaoDoComprovantePelaUnidadeEndpointTests : IAsyncLifetime
{
    private const string Media = "application/vnd.uniplus.comprovante-inscricao.v1+json";
    private const string Titular = "sub-comprovante-da-unidade";
    private const string QuemEmite = "isabella.nunes";
    private const string QuemSoConsulta = "leandro.viana";
    private const string QuemSoEmite = "betina.neves";
    private const string AvisoDaCancelada = "Documento sem validade: inscrição cancelada em 07/11/2026 às 22:32:05";

    private static readonly DateTimeOffset CanceladaEm = new(2026, 11, 8, 1, 32, 5, TimeSpan.Zero);

    private readonly CascadingFixture _fixture;

    public EmissaoDoComprovantePelaUnidadeEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { QuemEmite, QuemSoConsulta, QuemSoEmite })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Com consultar e emitir, a inscrição enviada sai no contrato do titular, sem cancelamento")]
    public async Task Enviada_Responde200SemCancelamento()
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage resposta = await EmitirAsync(QuemEmite, cenario.ProcessoId, cenario.Enviada);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be(Media);
        using JsonDocument json = JsonDocument.Parse(corpo);
        json.RootElement.GetProperty("inscricaoId").GetGuid().Should().Be(cenario.Enviada);
        json.RootElement.GetProperty("candidato").GetProperty("cpfMascarado").GetString().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        json.RootElement.TryGetProperty("cancelamento", out JsonElement cancelamento).Should().BeTrue();
        cancelamento.ValueKind.Should().Be(JsonValueKind.Null);
        corpo.Should().NotContain(PersonasDeTeste.FernandaDrumond.Valor);
    }

    [Fact(DisplayName = "A inscrição cancelada também sai, com o aviso, a data de cancelamento e o motivo")]
    public async Task Cancelada_Responde200ComCancelamento()
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage resposta = await EmitirAsync(QuemEmite, cenario.ProcessoId, cenario.Cancelada);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        JsonElement cancelamento = JsonDocument.Parse(corpo).RootElement.GetProperty("cancelamento");
        cancelamento.GetProperty("aviso").GetString().Should().Be(AvisoDaCancelada);
        cancelamento.GetProperty("canceladaEm").GetDateTimeOffset().Should().Be(CanceladaEm);
        cancelamento.GetProperty("motivo").GetString().Should().Be("Mudei de curso");
    }

    [Fact(DisplayName = "A inscrição cancelada sem motivo sai com o motivo nulo")]
    public async Task CanceladaSemMotivo_Responde200ComMotivoNulo()
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage resposta = await EmitirAsync(QuemEmite, cenario.ProcessoId, cenario.CanceladaSemMotivo);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        JsonElement cancelamento = JsonDocument.Parse(corpo).RootElement.GetProperty("cancelamento");
        cancelamento.GetProperty("motivo").ValueKind.Should().Be(JsonValueKind.Null);
        cancelamento.GetProperty("aviso").GetString().Should().Be(AvisoDaCancelada);
    }

    [Fact(DisplayName = "Quem consulta as inscrições mas não tem a permissão de emitir recebe 403, e quem só emite recebe 404")]
    public async Task SemAPermissaoDeEmitir_Recusa()
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemSoConsulta, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar);
        await ConcederAsync(QuemSoEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage semEmitir = await EmitirAsync(QuemSoConsulta, cenario.ProcessoId, cenario.Enviada);
        using HttpResponseMessage semConsultar = await EmitirAsync(QuemSoEmite, cenario.ProcessoId, cenario.Enviada);

        semEmitir.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        semConsultar.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A inscrição de outro processo responde 404, ainda que a pessoa tenha as duas permissões")]
    public async Task InscricaoDeOutroProcesso_Responde404()
    {
        Cenario cenario = await SemearAsync();
        Cenario outro = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage resposta = await EmitirAsync(QuemEmite, cenario.ProcessoId, outro.Enviada);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "O comprovante que a inscrição não tem responde 404")]
    public async Task NumeroInexistente_Responde404()
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.InscricoesEmitirComprovante);

        using HttpResponseMessage resposta = await EmitirAsync(QuemEmite, cenario.ProcessoId, cenario.Enviada, numero: 2);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("comprovante_nao_encontrado");
    }

    [Fact(DisplayName = "O titular continua sem comprovante da inscrição cancelada, que responde 422")]
    public async Task Titular_Cancelada_Responde422()
    {
        Cenario cenario = await SemearAsync();

        using HttpResponseMessage resposta = await ComoTitularAsync(cenario.Cancelada);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("comprovante_de_inscricao_cancelada");
    }

    [Theory(DisplayName = "A leitura administrativa da inscrição diz que pode emitir o comprovante só a quem tem a permissão de emitir")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LeituraAdministrativa_DizSePodeEmitir(bool podeEmitir)
    {
        Cenario cenario = await SemearAsync();
        PermissaoSelecao[] permissoes =
            [PermissaoSelecao.InscricoesConsultar, .. podeEmitir ? [PermissaoSelecao.InscricoesEmitirComprovante] : Array.Empty<PermissaoSelecao>()];
        await ConcederAsync(QuemEmite, cenario.UnidadeId, permissoes);

        using HttpResponseMessage resposta = await Requisitar(
            QuemEmite, $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{cenario.Enviada}", "inscricao-administrativa");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        JsonDocument.Parse(corpo).RootElement.GetProperty("podeEmitirComprovante").GetBoolean().Should().Be(podeEmitir);
    }

    [Theory(DisplayName = "A leitura administrativa traz o número do último comprovante, inclusive o da inscrição cancelada, sem depender de permissão de dado")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeituraAdministrativa_TrazONumeroDoUltimoComprovante(bool cancelada)
    {
        Cenario cenario = await SemearAsync();
        await ConcederAsync(QuemEmite, cenario.UnidadeId, PermissaoSelecao.InscricoesConsultar);

        using HttpResponseMessage resposta = await Requisitar(
            QuemEmite,
            $"/api/selecao/processos-seletivos/{cenario.ProcessoId}/inscricoes/{(cancelada ? cenario.Cancelada : cenario.Enviada)}",
            "inscricao-administrativa");

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        JsonDocument.Parse(corpo).RootElement.GetProperty("numeroDoUltimoComprovante").GetInt32().Should().Be(1);
    }

    private Task<HttpResponseMessage> EmitirAsync(string sub, Guid processoId, Guid inscricaoId, int numero = 1) =>
        Requisitar(sub, $"/api/selecao/processos-seletivos/{processoId}/inscricoes/{inscricaoId}/comprovantes/{numero}", "comprovante-inscricao");

    private Task<HttpResponseMessage> ComoTitularAsync(Guid inscricaoId) =>
        Requisitar(Titular, $"/api/selecao/inscricoes/{inscricaoId}/comprovantes/1", "comprovante-inscricao", PersonasDeTeste.FernandaDrumond.Valor);

    private async Task<HttpResponseMessage> Requisitar(string sub, string rota, string recurso, string? cpf = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        request.Headers.TryAddWithoutValidation("Accept", $"application/vnd.uniplus.{recurso}.v1+json");
        if (cpf is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, cpf);
        }

        return await client.SendAsync(request);
    }

    private async Task ConcederAsync(string sub, Guid unidade, params PermissaoSelecao[] permissoes)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, [.. permissoes.Select(static p => p.Codigo)]);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<Cenario> SemearAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (Guid processoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(api, client);
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid versaoId = await db.VersoesConfiguracao.AsNoTracking().Where(v => v.ProcessoSeletivoId == processoId).Select(v => v.Id).SingleAsync();
        Guid unidadeId = await db.ProcessosSeletivos.AsNoTracking().Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
        IInscricaoRepository repositorio = escopo.ServiceProvider.GetRequiredService<IInscricaoRepository>();
        Guid enviada = await SemearInscricaoAsync(repositorio, processoId, versaoId, SituacaoInscricao.Submetida, motivo: null);
        Guid cancelada = await SemearInscricaoAsync(repositorio, processoId, versaoId, SituacaoInscricao.Cancelada, "Mudei de curso");
        Guid semMotivo = await SemearInscricaoAsync(repositorio, processoId, versaoId, SituacaoInscricao.Cancelada, motivo: null);
        await escopo.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAlteracoesAsync();

        return new Cenario(processoId, unidadeId, enviada, cancelada, semMotivo);
    }

    private static async Task<Guid> SemearInscricaoAsync(
        IInscricaoRepository repositorio, Guid processoId, Guid versaoId, SituacaoInscricao situacao, string? motivo)
    {
        bool cancelada = situacao == SituacaoInscricao.Cancelada;
        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), processoId, PersonasDeTeste.FernandaDrumond, Titular, TestAuthHandler.DefaultIssuer, situacao, versaoId, null, null,
            "000321", cancelada ? CanceladaEm : null, cancelada ? Titular : null, motivo,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["CPF"] = JsonSerializer.SerializeToElement(PersonasDeTeste.FernandaDrumond.Valor) },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)
            {
                ["CPF"] = new(TipoDoValorDeResposta.Texto, Multivalorado: false, Identificador: true),
            },
            revisao: 1, DateTimeOffset.UtcNow, null);
        await repositorio.AdicionarAsync(inscricao);
        await repositorio.AdicionarComprovanteAsync(ComprovanteInscricao.Emitir(
            inscricao.Id, "000321", DateTimeOffset.UtcNow, null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME"] = JsonSerializer.SerializeToElement(PersonasDeTeste.NomeFernandaDrumond),
                ["DESEJA_NOME_SOCIAL"] = JsonSerializer.SerializeToElement(false),
            },
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal), [], []));

        return inscricao.Id;
    }

    private sealed record Cenario(Guid ProcessoId, Guid UnidadeId, Guid Enviada, Guid Cancelada, Guid CanceladaSemMotivo);
}
