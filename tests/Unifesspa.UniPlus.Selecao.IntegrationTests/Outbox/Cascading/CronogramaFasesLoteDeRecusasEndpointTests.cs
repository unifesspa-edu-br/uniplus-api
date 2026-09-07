namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O lote de recusas do cronograma chegando ao cliente, pelo HTTP: duas fases mal
/// declaradas produzem <b>duas</b> entradas em <c>errors[]</c>, cada uma com o campo que a
/// localiza.
/// </summary>
/// <remarks>
/// O teste precisa do HTTP inteiro porque o defeito que ele fecha vivia na costura: o
/// domínio acumulava as violações, e o handler as reduzia ao primeiro erro. Como
/// <c>errors[]</c> só é emitido quando alguma violação tem <c>field</c>, a resposta saía
/// sem array nenhum — e quem monta o edital corrigia uma fase, reenviava, e só então
/// descobria a outra. Nenhum teste que pare no handler enxerga isso.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CronogramaFasesLoteDeRecusasEndpointTests
{
    /// <summary>
    /// Duas fases do catálogo canônico, já semeadas, ligadas por uma aresta de precedência
    /// que a ordem 1→2 respeita, sem sobreposição de janelas; nenhuma das duas agrupa etapas.
    /// O código da fase canônica pertence a um vocabulário fechado, então não há como
    /// inventar um par privado para esta suíte.
    /// </summary>
    private const string CodigoFasePreliminar = "HETEROIDENTIFICACAO";
    private const string CodigoFaseDefinitiva = "HOMOLOGACAO_RESULTADO_FINAL";

    /// <summary>
    /// Os tipos de ato, ao contrário das fases, são cadastro aberto — o par privado evita
    /// que esta suíte dispute linha com as outras da mesma collection.
    /// </summary>
    private const string CodigoAtoPreliminar = "LOTE_RECUSAS_ATO_PRELIMINAR";
    private const string CodigoAtoDefinitivo = "LOTE_RECUSAS_ATO_DEFINITIVO";
    private const string CodigoAtoQueNaoEhResultado = "LOTE_RECUSAS_ATO_AVISO";

    private readonly CascadingFixture _fixture;

    public CronogramaFasesLoteDeRecusasEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Duas fases mal declaradas devolvem 422 com as DUAS violações em errors[], cada uma com o seu campo")]
    public async Task DuasFasesMalDeclaradas_422ComOsDoisErrosECampos()
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        await GarantirTiposDeAtoAsync(api);
        Guid fasePreliminarId = await ObterFaseCanonicaAsync(api, CodigoFasePreliminar);
        Guid faseDefinitivaId = await ObterFaseCanonicaAsync(api, CodigoFaseDefinitiva);
        Guid processoId = await SemearProcessoAsync(api);

        // A primeira publica preliminar e não declara quem a conclui; a segunda declara
        // concluinte sem publicar preliminar nenhuma. As duas são violações da MESMA
        // travessia, e o domínio as acumula.
        object[] fases =
        [
            new
            {
                ordem = 1,
                faseCanonicaId = fasePreliminarId,
                // As duas fases têm origem de data PRÓPRIA no catálogo e exigem janela; sem
                // ela a fábrica recusaria antes, e o lote sob prova nunca chegaria a rodar.
                inicio = new DateTimeOffset(2027, 1, 10, 0, 0, 0, TimeSpan.Zero),
                fim = new DateTimeOffset(2027, 1, 20, 0, 0, 0, TimeSpan.Zero),
                produtos = new object[] { new { atoCodigo = CodigoAtoPreliminar, papel = PapelProdutoFaseCodigo.Preliminar } },
                faseConcluinteCodigo = (string?)null,
                emiteParecerIndividual = false,
                tiposBancaIds = Array.Empty<Guid>(),
                regraRecurso = (object?)null,
            },
            new
            {
                ordem = 2,
                faseCanonicaId = faseDefinitivaId,
                inicio = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero),
                fim = new DateTimeOffset(2027, 2, 10, 0, 0, 0, TimeSpan.Zero),
                produtos = new object[] { new { atoCodigo = CodigoAtoDefinitivo, papel = PapelProdutoFaseCodigo.Definitivo } },
                faseConcluinteCodigo = "FASE_QUE_NAO_EXISTE",
                emiteParecerIndividual = false,
                tiposBancaIds = Array.Empty<Guid>(),
                regraRecurso = (object?)null,
            },
        ];

        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", UriKind.Relative))
        {
            Content = JsonContent.Create(fases),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        problema.RootElement.TryGetProperty("errors", out JsonElement erros).Should().BeTrue(
            "reduzir o lote ao primeiro erro apaga os campos, e sem campo nenhum a resposta sai sem errors[]");

        (string? Campo, string? Codigo)[] reportados = [.. erros.EnumerateArray()
            .Select(e => (
                Campo: e.GetProperty("field").GetString(),
                Codigo: e.GetProperty("code").GetString()))];

        reportados.Select(e => e.Campo).Should().BeEquivalentTo(
            ["fases[0].faseConcluinteCodigo", "fases[1].faseConcluinteCodigo"],
            "o operador precisa saber QUAIS fases corrigir, não só que alguma está errada");
        reportados.Select(e => e.Codigo).Should().BeEquivalentTo(
            [
                "uniplus.selecao.processo_seletivo.conclusao_nao_declarada",
                "uniplus.selecao.processo_seletivo.conclusao_declarada_sem_preliminar",
            ]);
        reportados.Should().NotContain(e => e.Campo == null, CadaElementoDeclaraOCaminho);
    }

    [Fact(DisplayName = "Recusa acumulada mais interrupção cross-módulo: os dois elementos de errors[] saem com field preenchido")]
    public async Task RecusaAcumuladaMaisInterrupcao_TodosOsElementosTemField()
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        await GarantirTiposDeAtoAsync(api);
        Guid fasePreliminarId = await ObterFaseCanonicaAsync(api, CodigoFasePreliminar);
        Guid faseDefinitivaId = await ObterFaseCanonicaAsync(api, CodigoFaseDefinitiva);
        Guid processoId = await SemearProcessoAsync(api);

        // A 1ª fase dá papel a um ato que o catálogo não classifica como resultado — recusa
        // que ACUMULA. A 2ª referencia um tipo de banca que não existe — resolução
        // cross-módulo, que INTERROMPE a passada levando o acumulado junto. É a combinação
        // que faz `errors[]` ser emitido: basta um item com campo para o array sair, e ele
        // é montado sobre todos.
        object[] fases =
        [
            new
            {
                ordem = 1,
                faseCanonicaId = fasePreliminarId,
                inicio = new DateTimeOffset(2027, 1, 10, 0, 0, 0, TimeSpan.Zero),
                fim = new DateTimeOffset(2027, 1, 20, 0, 0, 0, TimeSpan.Zero),
                produtos = new object[] { new { atoCodigo = CodigoAtoQueNaoEhResultado, papel = PapelProdutoFaseCodigo.Preliminar } },
                faseConcluinteCodigo = (string?)null,
                emiteParecerIndividual = false,
                tiposBancaIds = Array.Empty<Guid>(),
                regraRecurso = (object?)null,
            },
            new
            {
                ordem = 2,
                faseCanonicaId = faseDefinitivaId,
                inicio = new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero),
                fim = new DateTimeOffset(2027, 2, 10, 0, 0, 0, TimeSpan.Zero),
                produtos = new object[] { new { atoCodigo = CodigoAtoDefinitivo, papel = PapelProdutoFaseCodigo.Definitivo } },
                faseConcluinteCodigo = (string?)null,
                emiteParecerIndividual = false,
                tiposBancaIds = new[] { Guid.CreateVersion7() },
                regraRecurso = (object?)null,
            },
        ];

        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/cronograma-fases", UriKind.Relative))
        {
            Content = JsonContent.Create(fases),
        };
        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.TryGetProperty("errors", out JsonElement erros).Should().BeTrue();

        (string? Campo, string? Codigo)[] reportados = [.. erros.EnumerateArray()
            .Select(e => (
                Campo: e.GetProperty("field").GetString(),
                Codigo: e.GetProperty("code").GetString()))];

        reportados.Should().NotContain(e => e.Campo == null, CadaElementoDeclaraOCaminho);
        reportados.Select(e => e.Campo).Should().BeEquivalentTo(
            ["fases[0].produtos[0].papel", "fases[1].tiposBancaIds"]);
        reportados.Select(e => e.Codigo).Should().BeEquivalentTo(
            [
                "uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado",
                "uniplus.selecao.fase_cronograma.tipo_banca_nao_encontrado",
            ]);
    }

    private const string CadaElementoDeclaraOCaminho =
        "cada elemento de errors[] declara o caminho dot-notation do que falhou (ADR-0023) — " +
        "field: null manda o cliente procurar um campo que a resposta não nomeia";

    private static async Task GarantirTiposDeAtoAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.PublicacoesDbContext db =
            scope.ServiceProvider.GetRequiredService<Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.PublicacoesDbContext>();

        bool inseriu = false;
        foreach ((string codigo, string nome, bool ehResultado) in new[]
                 {
                     (CodigoAtoPreliminar, "Resultado preliminar do lote de recusas", true),
                     (CodigoAtoDefinitivo, "Resultado definitivo do lote de recusas", true),
                     (CodigoAtoQueNaoEhResultado, "Aviso do lote de recusas", false),
                 })
        {
            if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
            {
                continue;
            }

            Result<TipoAtoPublicado> tipo = TipoAtoPublicado.Criar(
                codigo, nome,
                congelaConfiguracao: false, unicoPorObjeto: false, efeitoIrreversivel: false, ehResultado: ehResultado,
                new DateOnly(2020, 1, 1), vigenciaFim: null, baseLegal: null);
            tipo.IsSuccess.Should().BeTrue(tipo.Error?.Message);
            await db.Set<TipoAtoPublicado>().AddAsync(tipo.Value!).ConfigureAwait(false);
            inseriu = true;
        }

        if (inseriu)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    private static async Task<Guid> ObterFaseCanonicaAsync(CascadingApiFactory api, string codigo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        ConfiguracaoDbContext config = scope.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();

        // O catálogo de fases vivas é pequeno — materializar é mais barato que traduzir a
        // comparação do value object Codigo para SQL.
        List<FaseCanonica> vivas = await config.FasesCanonicas.AsNoTracking().ToListAsync().ConfigureAwait(false);
        FaseCanonica? fase = vivas.Find(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal));

        fase.Should().NotBeNull($"'{codigo}' faz parte do catálogo canônico semeado");
        return fase!.Id;
    }

    private static async Task<Guid> SemearProcessoAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            $"PS lote de recusas {Guid.CreateVersion7()}",
            Unifesspa.UniPlus.Selecao.Domain.Enums.TipoProcesso.SiSU,
            OrigemCandidatos.ImportacaoExterna,
            Guid.CreateVersion7(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);

        await db.ProcessosSeletivos.AddAsync(processo).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return processo.Id;
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
