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
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// Os recursos administrativos do módulo decidem o acesso pela permissão concedida, e não pelo papel do token: quem tem
/// a concessão passa, o administrador da plataforma sem concessão e quem só tem a permissão de outro recurso não, e sem
/// login é 401. O recurso do processo recusa com o 404 do processo inexistente; o institucional, com 403.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class AcessoPorPermissaoNoModuloEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";
    private const string Etapa = "11111111-1111-1111-1111-111111111111";

    private readonly CascadingFixture _fixture;

    public AcessoPorPermissaoNoModuloEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public static TheoryData<string, string, string, PermissaoSelecao> RecursosDoProcesso => new()
    {
        { "formulários", "PUT", "/api/selecao/admin/processos-seletivos/{p}/formularios/INSCRICAO", PermissaoSelecao.ProcessosConfigurar },
        { "modelos de documento", "POST", "/api/selecao/processos-seletivos/{p}/modelos-de-documento", PermissaoSelecao.ProcessosConfigurar },
        { "produtos previstos", "GET", "/api/selecao/processos-seletivos/{p}/produtos-previstos", PermissaoSelecao.ProcessosAcompanhar },
        { "atos (consulta)", "GET", "/api/selecao/processos-seletivos/{p}/atos", PermissaoSelecao.ProcessosAcompanhar },
        { "atos (publicação)", "POST", "/api/selecao/processos-seletivos/{p}/atos", PermissaoSelecao.ProcessosPublicar },
        { "documentos a publicar", "GET", "/api/selecao/processos-seletivos/{p}/documentos-a-publicar", PermissaoSelecao.ProcessosPublicar },
        { "envios ao INEP", "GET", "/api/selecao/processos-seletivos/{p}/etapas/" + Etapa + "/edicoes-do-enem/2024/envios-ao-inep", PermissaoSelecao.EnviosInepGerar },
        { "notas do ENEM", "GET", "/api/selecao/processos-seletivos/{p}/etapas/" + Etapa + "/edicoes-do-enem/2024/lotes-de-notas", PermissaoSelecao.NotasEnemImportar },
    };

    public static TheoryData<string, string, string, PermissaoSelecao> RecursosInstitucionais => new()
    {
        { "obrigatoriedades legais", "POST", "/api/selecao/admin/obrigatoriedades-legais", PermissaoSelecao.ObrigatoriedadesLegaisManter },
        { "catálogo de regras", "GET", "/api/selecao/regras-catalogo", PermissaoSelecao.RegrasCatalogoConsultar },
        { "leiautes do INEP", "GET", "/api/selecao/leiautes-de-arquivo-do-inep", PermissaoSelecao.LeiautesInepConsultar },
    };

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory(DisplayName = "Recurso do processo: a permissão na unidade dona abre")]
    [MemberData(nameof(RecursosDoProcesso))]
    public async Task Processo_ComPermissao_Passa(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        (Guid processo, Guid unidade) = await SemearAsync();
        await ConcederNaUnidadeAsync(Leandro, unidade, permissao);

        using HttpResponseMessage resposta = await EnviarAsync(Leandro, metodo, Caminho(rota, processo));

        ChegouAoRecurso(resposta, processo, permissao, Motivo(recurso, permissao));
    }

    [Theory(DisplayName = "Recurso do processo: o administrador da plataforma sem concessão e a permissão de outro recurso respondem 404")]
    [MemberData(nameof(RecursosDoProcesso))]
    public async Task Processo_SemPermissao_404(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        (Guid processo, Guid unidade) = await SemearAsync();
        await ConcederNaUnidadeAsync(Leandro, unidade, PermissaoSelecao.InscricoesConsultar);
        string caminho = Caminho(rota, processo);
        string motivo = Motivo(recurso, permissao);

        using HttpResponseMessage administrador = await EnviarAsync(Diogo, metodo, caminho, "plataforma-admin");
        using HttpResponseMessage outroRecurso = await EnviarAsync(Leandro, metodo, caminho);

        administrador.StatusCode.Should().Be(HttpStatusCode.NotFound, motivo);
        outroRecurso.StatusCode.Should().Be(HttpStatusCode.NotFound, motivo);
        (await administrador.Content.ReadAsStringAsync()).Should().Contain(processo.ToString());
    }

    [Theory(DisplayName = "Recurso do processo: sem login responde 401")]
    [MemberData(nameof(RecursosDoProcesso))]
    public async Task Processo_SemLogin_401(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        (Guid processo, _) = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(null, metodo, Caminho(rota, processo));

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized, Motivo(recurso, permissao));
    }

    [Theory(DisplayName = "Recurso institucional: a concessão abre")]
    [MemberData(nameof(RecursosInstitucionais))]
    public async Task Institucional_ComPermissao_Passa(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, permissao.Codigo);

        using HttpResponseMessage resposta = await EnviarAsync(Leandro, metodo, rota);

        resposta.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, Motivo(recurso, permissao)).And.NotBe(HttpStatusCode.Unauthorized, Motivo(recurso, permissao));
    }

    [Theory(DisplayName = "Recurso institucional: o administrador da plataforma sem concessão e a permissão de outro recurso respondem 403")]
    [MemberData(nameof(RecursosInstitucionais))]
    public async Task Institucional_SemPermissao_403(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(
            _fixture.Factory.Services, Leandro, PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo);

        using HttpResponseMessage administrador = await EnviarAsync(Diogo, metodo, rota, "plataforma-admin");
        using HttpResponseMessage outroRecurso = await EnviarAsync(Leandro, metodo, rota);

        administrador.StatusCode.Should().Be(HttpStatusCode.Forbidden, Motivo(recurso, permissao));
        outroRecurso.StatusCode.Should().Be(HttpStatusCode.Forbidden, Motivo(recurso, permissao));
    }

    [Theory(DisplayName = "Recurso institucional: sem login responde 401")]
    [MemberData(nameof(RecursosInstitucionais))]
    public async Task Institucional_SemLogin_401(string recurso, string metodo, string rota, PermissaoSelecao permissao)
    {
        using HttpResponseMessage resposta = await EnviarAsync(null, metodo, rota);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized, Motivo(recurso, permissao));
    }

    private static string Motivo(string recurso, PermissaoSelecao permissao) => $"{recurso} ({permissao.Codigo})";

    private static string Caminho(string rota, Guid processo)
    {
        ArgumentNullException.ThrowIfNull(rota);
        return rota.Replace("{p}", processo.ToString(), StringComparison.Ordinal);
    }

    private void ChegouAoRecurso(HttpResponseMessage resposta, Guid processo, PermissaoSelecao permissao, string motivo)
    {
        resposta.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, motivo).And.NotBe(HttpStatusCode.Forbidden, motivo);
        string codigo = permissao.Codigo;
        List<RegistroDecisaoAcesso> decisoes = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>().Registrados
            .Where(r => r.ProcessoId == processo && r.Permissao == codigo)];
        decisoes.Should().NotBeEmpty(motivo).And.OnlyContain(r => r.Permitido, motivo);
    }

    private async Task<(Guid ProcessoId, Guid UnidadeId)> SemearAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Acesso por permissão {Guid.CreateVersion7()}");
        Guid unidade = await db.ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processo.Id).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
        return (processo.Id, unidade);
    }

    private async Task ConcederNaUnidadeAsync(string sub, Guid unidade, params PermissaoSelecao[] permissoes)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, [.. permissoes.Select(static p => p.Codigo)]);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<HttpResponseMessage> EnviarAsync(string? sub, string metodo, string rota, string? papel = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(metodo), new Uri(rota, UriKind.Relative));
        if (metodo != "GET")
        {
            request.Content = JsonContent.Create(new { });
        }

        if (sub is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
            if (papel is not null)
            {
                request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
            }
        }

        return await client.SendAsync(request);
    }
}
