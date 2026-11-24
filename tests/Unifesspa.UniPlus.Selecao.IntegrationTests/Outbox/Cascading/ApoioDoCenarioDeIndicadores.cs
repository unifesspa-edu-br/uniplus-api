namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

internal sealed class ApoioDoCenarioDeIndicadores
{
    public const string Rota = "/api/selecao/processos-seletivos";
    public const string MediaType = "application/vnd.uniplus.indicadores-do-processo.v1+json";
    public const string Pessoa = "betina.neves";
    public const string SubInscrito = "indicadores.servidor.inscrito";
    private readonly CascadingFixture _ambiente;

    public ApoioDoCenarioDeIndicadores(CascadingFixture cenario) => _ambiente = cenario;

    public async Task LimparConcessoesAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_ambiente.Factory.Services, Pessoa);
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_ambiente.Factory.Services, SubInscrito);
    }

    public static async Task ConferirContagensAsync(HttpResponseMessage resposta, params long[] esperadas)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] indicadores = [.. corpo.RootElement.GetProperty("indicadores").EnumerateArray()];
        indicadores.Select(i => i.GetProperty("valor").GetInt64()).Should().Equal(esperadas);
    }

    public async Task<Guid> SemearRascunhoAsync(string nome)
    {
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(banco, new() { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return processo.Id;
    }

    public async Task<Guid> SemearPublicadoAsync(string nome, DateTimeOffset? fimDasInscricoes = null)
    {
        await TiposDeAtoSeeder.SemearAsync(_ambiente.Factory.Services);
        if (fimDasInscricoes is not null)
            return await PublicarEditalCompletoAsync(fimDasInscricoes.Value);

        (Guid processoId, Guid documentoId) = await SemearProcessoComDocumentoAsync(nome);
        return await PublicarProcessoSemJanelaAsync(processoId, documentoId);
    }

    private async Task<Guid> PublicarProcessoSemJanelaAsync(Guid processoId, Guid documentoId)
    {
        using HttpResponseMessage publicada = await EnviarAsync(
            null, $"{Rota}/{processoId}/publicacao", null,
            new(Papel: "plataforma-admin", Metodo: HttpMethod.Post, Corpo: CorpoDaPublicacao(documentoId)));
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        return processoId;
    }

    private async Task<Guid> PublicarEditalCompletoAsync(DateTimeOffset fimDasInscricoes)
    {
        using HttpClient cliente = _ambiente.Factory.CreateClient();
        (Guid processoPublicadoId, _) = await PublicarAtoAvulsoEndpointTests.PublicarEditalAsync(
            _ambiente.Factory, cliente, fimDasInscricoes: fimDasInscricoes);
        return processoPublicadoId;
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComDocumentoAsync(string nome)
    {
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) =
            await ProcessoSeletivoPublicavelSeeder.SemearAsync(banco, new() { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private static object CorpoDaPublicacao(Guid documentoId) => new
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

    public async Task ConcederAsync(string sub, Guid processoId, string codigoPermissao)
    {
        Guid unidade = await UnidadeDoProcessoAsync(processoId);
        await ConcederNaUnidadeAsync(sub, unidade, codigoPermissao);
    }

    public async Task ConcederNaUnidadeAsync(string sub, Guid unidadeId, string codigoPermissao)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_ambiente.Factory.Services, codigoPermissao);
        await ConcessoesDeTeste.ConcederAsync(
            _ambiente.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidadeId));
    }

    private async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope escopo = _ambiente.Factory.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    public async Task<HttpResponseMessage> EnviarAsync(
        string? sub,
        string rota,
        string? tipoAceito,
        OpcoesDeRequisicao? opcoes = null)
    {
        using HttpClient cliente = _ambiente.Factory.CreateClient();
        using HttpRequestMessage requisicao = CriarRequisicao(sub, rota, tipoAceito, opcoes);
        return await cliente.SendAsync(requisicao);
    }

    private static HttpRequestMessage CriarRequisicao(
        string? sub,
        string rota,
        string? tipoAceito,
        OpcoesDeRequisicao? opcoes)
    {
        HttpRequestMessage requisicao = new(opcoes?.Metodo ?? HttpMethod.Get, new Uri(rota, UriKind.Relative));
        if (opcoes?.Corpo is not null)
            requisicao.Content = JsonContent.Create(opcoes.Corpo);
        ConfigurarIdentidade(requisicao, sub, opcoes);
        if (tipoAceito is not null)
            requisicao.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(tipoAceito));
        ConfigurarIdempotencia(requisicao, opcoes);
        return requisicao;
    }

    private static void ConfigurarIdentidade(HttpRequestMessage requisicao, string? sub, OpcoesDeRequisicao? opcoes)
    {
        if (sub is not null || opcoes?.Papel is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        if (sub is not null)
            requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        if (opcoes?.Cpf is not null)
            requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, opcoes.Cpf);
        if (opcoes?.Papel is not null)
            requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, opcoes.Papel);
    }

    private static void ConfigurarIdempotencia(HttpRequestMessage requisicao, OpcoesDeRequisicao? opcoes)
    {
        if (opcoes?.Metodo is not null && opcoes.Metodo != HttpMethod.Get)
            requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        if (opcoes?.IfMatch is not null)
            requisicao.Headers.TryAddWithoutValidation("If-Match", opcoes.IfMatch);
    }

    public sealed record OpcoesDeRequisicao(
        string? Papel = null,
        HttpMethod? Metodo = null,
        object? Corpo = null,
        string? Cpf = null,
        string? IfMatch = null);
}
