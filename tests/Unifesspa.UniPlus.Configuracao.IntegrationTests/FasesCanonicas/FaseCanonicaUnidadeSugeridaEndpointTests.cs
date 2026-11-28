namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.FasesCanonicas.ApoioDosEndpointsDeFaseCanonica;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FaseCanonicaUnidadeSugeridaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public FaseCanonicaUnidadeSugeridaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    private Task LiberarSlotDoSeedAsync(string codigo) =>
        ApoioDosEndpointsDeFaseCanonica.LiberarSlotDoSeedAsync(_fixture.ConnectionString, codigo);

    [Fact(DisplayName = "POST com unidade sugerida viva guarda a cópia de sigla e nome e o GET a devolve")]
    public async Task Criar_UnidadeSugeridaViva_GetDevolveACopia()
    {
        await LiberarSlotDoSeedAsync("HABILITACAO");
        Unidade cga = await SemearUnidadeAsync("Centro de Gestão Acadêmica", "CGA");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarFaseAsync(client, new CorpoDoTipoDeFase { Codigo = "HABILITACAO", Nome = "Habilitação", OrigemData = "NA_EXECUCAO", UnidadeSugeridaId = cga.Id });

        JsonElement unidade = (await ObterAsync(client, id)).GetProperty("unidadeSugerida");
        unidade.GetProperty("origemId").GetGuid().Should().Be(cga.Id);
        unidade.GetProperty("sigla").GetString().Should().Be(cga.Sigla);
        unidade.GetProperty("nome").GetString().Should().Be("Centro de Gestão Acadêmica");
    }

    [Fact(DisplayName = "POST sem unidade sugerida cria a fase e o GET não traz a unidade")]
    public async Task Criar_SemUnidadeSugerida_GetNaoTrazAUnidade()
    {
        await LiberarSlotDoSeedAsync("CLASSIFICACAO");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new CorpoDoTipoDeFase
        {
            Codigo = "CLASSIFICACAO",
            Nome = "Classificação",
            OrigemData = "NA_EXECUCAO",
        });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        (await ObterAsync(client, id)).GetProperty("unidadeSugerida").ValueKind
            .Should().Be(JsonValueKind.Null, "a fase sem sugestão não carrega unidade");
    }

    [Fact(DisplayName = "POST com unidade sugerida inexistente retorna 422 no campo unidadeSugeridaId")]
    public async Task Criar_UnidadeSugeridaInexistente_Retorna422NoCampo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, new CorpoDoTipoDeFase
        {
            Codigo = "MATRICULA",
            Nome = "Matrícula",
            OrigemData = "NA_EXECUCAO",
            UnidadeSugeridaId = Guid.NewGuid(),
        });

        await ConferirRecusaDaUnidadeSugeridaAsync(response);
    }

    [Fact(DisplayName = "POST com unidade sugerida inativa retorna 422 no campo unidadeSugeridaId")]
    public async Task Criar_UnidadeSugeridaInativa_Retorna422NoCampo()
    {
        Unidade extinta = await SemearUnidadeAsync("Coordenação extinta", "COEXT", inativa: true);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, new CorpoDoTipoDeFase
        {
            Codigo = "MATRICULA",
            Nome = "Matrícula",
            OrigemData = "NA_EXECUCAO",
            UnidadeSugeridaId = extinta.Id,
        });

        await ConferirRecusaDaUnidadeSugeridaAsync(response);
    }

    [Fact(DisplayName = "PUT troca a unidade sugerida e PUT com unidadeSugeridaId nulo a retira")]
    public async Task Atualizar_TrocaERetiraAUnidadeSugerida()
    {
        await LiberarSlotDoSeedAsync("ENSALAMENTO");
        Unidade cga = await SemearUnidadeAsync("Centro de Gestão Acadêmica", "CGA");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarFaseAsync(client, new CorpoDoTipoDeFase { Codigo = "ENSALAMENTO", Nome = "Ensalamento", OrigemData = "NA_EXECUCAO" });

        await SugerirUnidadeAsync(client, id, cga.Id);
        (await ObterAsync(client, id)).GetProperty("unidadeSugerida").GetProperty("sigla").GetString().Should().Be(cga.Sigla);
        await SugerirUnidadeAsync(client, id, null);

        (await ObterAsync(client, id)).GetProperty("unidadeSugerida").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static async Task<Guid> CriarFaseAsync(HttpClient client, object corpo)
    {
        using HttpResponseMessage criar = await EnviarPostAdmin(client, corpo);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task SugerirUnidadeAsync(HttpClient client, Guid id, Guid? unidadeSugeridaId)
    {
        using HttpResponseMessage atualizar = await EnviarPutAdmin(
            client, id, new CorpoDoTipoDeFase { Id = id, Nome = "Ensalamento", OrigemData = "NA_EXECUCAO", UnidadeSugeridaId = unidadeSugeridaId });

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task ConferirRecusaDaUnidadeSugeridaAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.fase_canonica.unidade_sugerida_nao_encontrada");
        doc.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().Contain("unidadeSugeridaId");
    }

    private async Task<Unidade> SemearUnidadeAsync(string nome, string siglaBase, bool inativa = false)
    {
        Unidade unidade = NovaUnidade(nome, siglaBase);
        if (inativa)
        {
            unidade.MarkAsDeleted("teste", DateTimeOffset.UtcNow);
        }

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext dbContext = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        dbContext.Unidades.Add(unidade);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return unidade;
    }

    private static Unidade NovaUnidade(string nome, string siglaBase)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        IdentificadoresDaUnidade identificadores = new(
            $"{siglaBase.ToLowerInvariant()}-{sufixo}", $"{siglaBase}{sufixo[..4]}".ToUpperInvariant(), $"FSC{sufixo}".ToUpperInvariant(), null);

        return Unidade.Criar(
            nome, identificadores, CidadeDaUnidade.Nenhuma, new VigenciaDaUnidade(new DateOnly(2026, 1, 1), null), null, TipoUnidade.Centro, false).Value!;
    }
}
