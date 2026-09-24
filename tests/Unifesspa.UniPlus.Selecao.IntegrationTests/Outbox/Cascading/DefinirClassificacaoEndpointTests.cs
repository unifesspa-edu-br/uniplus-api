namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O endpoint <c>PUT /processos-seletivos/{id}/classificacao</c>, fim a fim pelo HTTP —
/// prova a obrigatoriedade real de <c>BaseadoEmEnem</c> na desserialização (issue #850,
/// CA-10): o host não habilita <c>RespectRequiredConstructorParameters</c>
/// (<c>Program.cs</c> só configura naming policy e enum converter), então por padrão do
/// <c>System.Text.Json</c> um parâmetro de construtor de record ausente no corpo recebe o
/// valor default (<see langword="false"/>) — indistinguível de um <see langword="false"/>
/// explícito. <c>[property: JsonRequired]</c> em <c>DefinirClassificacaoRequest</c> fecha
/// essa lacuna: omitir o campo vira <c>400</c>, nunca um <c>false</c> silencioso.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class DefinirClassificacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public DefinirClassificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Corpo sem baseadoEmEnem é rejeitado com 400 — não vira false silencioso")]
    public async Task SemBaseadoEmEnem_400()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(SemBaseadoEmEnem_400));

        const string corpoSemCampo = """
            {
              "regraCalculoCodigo": "CLASSIFICACAO-IMPORTADA",
              "regraCalculoVersao": "v1",
              "regraArredondamentoCodigo": null,
              "regraArredondamentoVersao": null,
              "casasArredondamento": null,
              "regraOrdemAlocacaoCodigo": "ALOCACAO-OPCOES-RN04",
              "regraOrdemAlocacaoVersao": "v1",
              "nOpcoesAlocacao": 1,
              "regrasEliminacao": []
            }
            """;

        HttpResponseMessage resposta = await ctx.PutClassificacaoRawAsync(corpoSemCampo);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "baseadoEmEnem omitido não pode desserializar como false implícito — o parâmetro é [JsonRequired]");
    }

    [Fact(DisplayName = "baseadoEmEnem=false explícito é aceito e persiste false")]
    public async Task ComBaseadoEmEnemFalseExplicito_204EPersisteFalse()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComBaseadoEmEnemFalseExplicito_204EPersisteFalse));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoImportadaSemEliminacao(baseadoEmEnem: false));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "baseadoEmEnem=true explícito é aceito e persiste true")]
    public async Task ComBaseadoEmEnemTrueExplicito_204EPersisteTrue()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ComBaseadoEmEnemTrueExplicito_204EPersisteTrue));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoImportadaSemEliminacao(baseadoEmEnem: true));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "Resolução completa congela os quatro grupos, e editar o cadastro depois não altera o processo")]
    public async Task ResolucaoCompleta_CongelaEEdicaoPosteriorDoCadastroNaoAlcancaOProcesso()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ResolucaoCompleta_CongelaEEdicaoPosteriorDoCadastroNaoAlcancaOProcesso));
        string resolucao = await PesosAreaEnemSeeder.SemearResolucaoAsync(ctx.Api);

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoEnemLocal(resolucao));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        (await QuadroCongeladoAsync(ctx)).Should().BeEquivalentTo(
        [
            ("HUMANISTICA_I", "REDACAO", 2.00m),
            ("HUMANISTICA_II", "REDACAO", 2.00m),
            ("SAUDE_E_BIOLOGICAS", "REDACAO", 2.00m),
            ("TECNOLOGICA", "REDACAO", 2.00m),
        ]);

        await using (AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope())
        {
            ConfiguracaoDbContext config = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
            List<PesoAreaEnem> linhas = await config.PesosAreaEnem.Where(p => p.Resolucao == resolucao).ToListAsync();
            PesoAreaEnem saude = linhas.Single(p => p.GrupoCurso.Codigo == GrupoCurso.SaudeEBiologicas);
            saude.Atualizar(
                [
                    new AreaInformada(PesoAreaEnem.CodigoRedacao, 3.00m, 500m),
                    new AreaInformada(PesoAreaEnem.CodigoCienciasDaNatureza, 1.50m, null),
                    new AreaInformada(PesoAreaEnem.CodigoCienciasHumanas, 2.50m, null),
                    new AreaInformada(PesoAreaEnem.CodigoLinguagens, 2.50m, null),
                    new AreaInformada(PesoAreaEnem.CodigoMatematica, 1.50m, null),
                ],
                "Resolução nº 805/2024/Consepe – Anexo I (alterada)").IsSuccess.Should().BeTrue();
            await config.SaveChangesAsync();
        }

        (await QuadroCongeladoAsync(ctx)).Should().Contain(("SAUDE_E_BIOLOGICAS", "REDACAO", 2.00m),
            "o processo guarda a cópia feita quando a classificação foi definida; o cadastro editado não o alcança");
    }

    [Fact(DisplayName = "Resolução sem um dos grupos vivos é recusada com 422 nomeando o grupo ausente")]
    public async Task ResolucaoIncompleta_422NomeiaOGrupoAusente()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ResolucaoIncompleta_422NomeiaOGrupoAusente));
        string resolucao = await PesosAreaEnemSeeder.SemearResolucaoAsync(
            ctx.Api, [GrupoCurso.Tecnologica, GrupoCurso.HumanisticaI, GrupoCurso.HumanisticaII]);

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoEnemLocal(resolucao));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        string[] textos = [.. Textos(problema.RootElement)];
        textos.Should().Contain("uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_incompleta");
        textos.Should().Contain(texto => texto.Contains("Saúde e Biológicas", StringComparison.Ordinal),
            "sem o nome do grupo o operador não sabe o que cadastrar");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.BaseadoEmEnem.Should().BeFalse("a recusa não grava nada: fica a classificação que o processo já tinha");
    }

    [Fact(DisplayName = "Resolução enviada em forma decomposta casa com o cadastro, e o processo guarda a forma do cadastro")]
    public async Task ResolucaoDecomposta_CasaComOCadastroEGuardaNfc()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ResolucaoDecomposta_CasaComOCadastroEGuardaNfc));
        string doCadastro = await PesosAreaEnemSeeder.SemearResolucaoAsync(ctx.Api, prefixo: "Resolução");
        string decomposta = doCadastro.Normalize(System.Text.NormalizationForm.FormD);
        decomposta.Should().NotBe(doCadastro, "pré-condição: a resolução tem acento, e as duas formas diferem");

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoEnemLocal(decomposta));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.ResolucaoPesoAreaEnem.Should().Be(doCadastro);
    }

    [Fact(DisplayName = "Resolução com caractere nulo é recusada com 422, não com erro de banco")]
    public async Task ResolucaoComCaractereNulo_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ResolucaoComCaractereNulo_422));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoEnemLocal("805\u0000"));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "o caractere nulo chegaria ao Postgres como parâmetro da busca no cadastro, que o recusa com erro de banco");
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Textos(problema.RootElement).Should().Contain("uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_invalida");
    }

    [Fact(DisplayName = "Resolução com não-caractere é recusada com 422, não com erro 500")]
    public async Task ResolucaoComNaoCaractere_422()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(ResolucaoComNaoCaractere_422));

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoEnemLocal("Res. 805" + (char)0xFFFE));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        Textos(problema.RootElement).Should().Contain("uniplus.selecao.configuracao_classificacao.resolucao_peso_area_enem_invalida");
    }

    [Fact(DisplayName = "Desmarcar BaseadoEmEnem descarta a resolução e o quadro congelado")]
    public async Task DesmarcarBaseadoEmEnem_DescartaOQuadro()
    {
        Contexto ctx = await SemearRascunhoAsync(nameof(DesmarcarBaseadoEmEnem_DescartaOQuadro));
        string resolucao = await PesosAreaEnemSeeder.SemearResolucaoAsync(ctx.Api);
        (await ctx.PutClassificacaoAsync(CorpoEnemLocal(resolucao))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await QuadroCongeladoAsync(ctx)).Should().HaveCount(4);

        HttpResponseMessage resposta = await ctx.PutClassificacaoAsync(CorpoImportadaSemEliminacao(baseadoEmEnem: false));

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await QuadroCongeladoAsync(ctx)).Should().BeEmpty("sem ENEM não há quadro, e nenhum grupo fica órfão");

        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ConfiguracaoClassificacao classificacao = await db.Set<ConfiguracaoClassificacao>().AsNoTracking()
            .SingleAsync(c => c.ProcessoSeletivoId == ctx.ProcessoId);
        classificacao.ResolucaoPesoAreaEnem.Should().BeNull();
    }

    /// <summary>
    /// A Redação de cada grupo congelado do processo, lida direto do banco do Seleção — é a
    /// cópia que o processo guarda, não o cadastro.
    /// </summary>
    private static async Task<List<(string Grupo, string Area, decimal Peso)>> QuadroCongeladoAsync(Contexto ctx)
    {
        await using AsyncServiceScope scope = ctx.Api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<GrupoPesoAreaEnemCongelado> grupos = await db.Set<GrupoPesoAreaEnemCongelado>().AsNoTracking()
            .Include(g => g.Areas)
            .Where(g => db.Set<ConfiguracaoClassificacao>()
                .Any(c => c.Id == g.ConfiguracaoClassificacaoId && c.ProcessoSeletivoId == ctx.ProcessoId))
            .ToListAsync();

        return [.. grupos.Select(g => (g.GrupoAreaEnem.Codigo, "REDACAO", g.Areas.Single(a => a.Codigo == "REDACAO").Peso))];
    }

    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.String => [elemento.GetString()!],
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(propriedade => Textos(propriedade.Value)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        _ => [],
    };

    private static object CorpoEnemLocal(string resolucao) => new
    {
        regraCalculoCodigo = RegraCalculoCodigo.FormulaMediaPonderada,
        regraCalculoVersao = "v1",
        regraArredondamentoCodigo = RegraArredondamentoCodigo.PrecisaoTruncar,
        regraArredondamentoVersao = "v1",
        casasArredondamento = 2,
        regraOrdemAlocacaoCodigo = RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04,
        regraOrdemAlocacaoVersao = "v1",
        nOpcoesAlocacao = 1,
        regrasEliminacao = Array.Empty<object>(),
        baseadoEmEnem = true,
        resolucaoPesoAreaEnem = resolucao,
    };

    private static object CorpoImportadaSemEliminacao(bool baseadoEmEnem) => new
    {
        regraCalculoCodigo = "CLASSIFICACAO-IMPORTADA",
        regraCalculoVersao = "v1",
        regraArredondamentoCodigo = (string?)null,
        regraArredondamentoVersao = (string?)null,
        casasArredondamento = (int?)null,
        regraOrdemAlocacaoCodigo = "ALOCACAO-OPCOES-RN04",
        regraOrdemAlocacaoVersao = "v1",
        nOpcoesAlocacao = 1,
        regrasEliminacao = Array.Empty<object>(),
        baseadoEmEnem,
    };

    private sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId)
    {
        public async Task<HttpResponseMessage> PutClassificacaoAsync(object corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/classificacao", UriKind.Relative))
            {
                Content = JsonContent.Create(corpo),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> PutClassificacaoRawAsync(string corpoJson)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/classificacao", UriKind.Relative))
            {
                Content = new StringContent(corpoJson, Encoding.UTF8, "application/json"),
            };
            Autenticar(request);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }
    }

    private async Task<Contexto> SemearRascunhoAsync(string nome)
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        return new Contexto(api, client, processoId);
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
