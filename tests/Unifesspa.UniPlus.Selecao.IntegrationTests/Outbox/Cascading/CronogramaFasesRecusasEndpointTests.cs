namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCronogramaDeFasesPeloEndpoint;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

/// <summary>
/// As recusas do cronograma chegando ao cliente, pelo HTTP: duas fases mal
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
public sealed class CronogramaFasesRecusasEndpointTests
{
    /// <summary>
    /// Duas fases do catálogo canônico, já semeadas, ligadas por uma aresta de precedência
    /// que a ordem 1→2 respeita, sem sobreposição de períodos; nenhuma das duas agrupa etapas.
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

    /// <summary>
    /// Fase canônica de data definida na execução: o cronograma aceita só a data final.
    /// </summary>
    private const string CodigoFaseDeDataNaExecucao = "CHAMADA";

    private const string CodigoPapelEmAtoQueNaoEhResultado =
        "uniplus.selecao.produto_da_fase.papel_em_ato_que_nao_eh_resultado";

    private const string CadaElementoDeclaraOCaminho =
        "cada elemento de errors[] declara o caminho dot-notation do que falhou (ADR-0023) — " +
        "field: null manda o cliente procurar um campo que a resposta não nomeia";

    private static readonly (string Codigo, string Nome, bool EhResultado)[] TiposDeAtoDoCenario =
    [
        (CodigoAtoPreliminar, "Resultado preliminar das recusas", true),
        (CodigoAtoDefinitivo, "Resultado definitivo das recusas", true),
        (CodigoAtoQueNaoEhResultado, "Aviso das recusas", false),
    ];

    // As duas fases têm a data fixada no edital (NO_EDITAL) e exigem o período; sem ele a fábrica
    // recusaria antes, e o conjunto de recusas sob prova nunca chegaria a ser conferido.
    private static readonly PeriodoDaFase PeriodoDeJaneiro = new(
        new DateTimeOffset(2027, 1, 10, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 20, 0, 0, 0, TimeSpan.Zero));

    private static readonly PeriodoDaFase PeriodoDeFevereiro = new(
        new DateTimeOffset(2027, 2, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 2, 10, 0, 0, 0, TimeSpan.Zero));

    private readonly CascadingFixture _fixture;

    public CronogramaFasesRecusasEndpointTests(CascadingFixture ambienteDeTeste) => _fixture = ambienteDeTeste;

    [Fact(DisplayName = "Duas fases mal declaradas devolvem 422 com as DUAS violações em errors[], cada uma com o seu campo")]
    public async Task DuasFasesMalDeclaradas_422ComOsDoisErrosECampos()
    {
        CenarioDoCronograma cenario = await PrepararCenarioAsync();
        object[] fases = FasesComPapelEmAtoQueNaoEhResultado(cenario);

        ErroDoProblema[] reportados = await EnviarERecusarAsync(
            cenario, fases, "reduzir as recusas ao primeiro erro apaga os campos, e sem campo nenhum a resposta sai sem errors[]");

        AfirmarCampoECodigoDeCadaErro(
            reportados,
            [
                new ErroDoProblema("fases[0].produtos[0].papel", CodigoPapelEmAtoQueNaoEhResultado),
                new ErroDoProblema("fases[1].produtos[0].papel", CodigoPapelEmAtoQueNaoEhResultado),
            ],
            motivoDosCampos: "o operador precisa saber QUAIS fases corrigir, não só que alguma está errada");
    }

    [Fact(DisplayName = "Recusa acumulada mais interrupção cross-módulo: os dois elementos de errors[] saem com field preenchido")]
    public async Task RecusaAcumuladaMaisInterrupcao_TodosOsElementosTemField()
    {
        CenarioDoCronograma cenario = await PrepararCenarioAsync();
        object[] fases = FasesComRecusaAcumuladaEInterrupcao(cenario);

        ErroDoProblema[] reportados = await EnviarERecusarAsync(cenario, fases);

        AfirmarCampoECodigoDeCadaErro(
            reportados,
            [
                new ErroDoProblema("fases[0].produtos[0].papel", CodigoPapelEmAtoQueNaoEhResultado),
                new ErroDoProblema("fases[1].bancasRequeridas[0].tipoBancaId", "uniplus.selecao.fase_cronograma.tipo_banca_nao_encontrado"),
            ]);
    }

    [Fact(DisplayName = "A recusa da gravação do cronograma aponta a banca só pela posição e nunca traz identificadores")]
    public async Task RecusaDaGravacao_ApontaABancaSoPelaPosicao_SemIdentificadores()
    {
        CenarioDoCronograma cenario = await PrepararCenarioAsync();

        using HttpResponseMessage resposta = await EnviarCronogramaAsync(cenario.Client, cenario.ProcessoId, FasesComRecusaAcumuladaEInterrupcao(cenario));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Should().Contain(static erro => erro.GetProperty("field").GetString() == "fases[1].bancasRequeridas[0].tipoBancaId");
        erros.Count(static erro => erro.TryGetProperty("identificadores", out _)).Should().Be(0, "a gravação aponta pela posição do corpo enviado");
    }

    // As duas fases dão papel a um ato que o catálogo não classifica como resultado. São
    // violações da MESMA travessia, e o domínio as acumula.
    private static object[] FasesComPapelEmAtoQueNaoEhResultado(CenarioDoCronograma cenario) =>
    [
        FaseDoCronograma(1, cenario.FasePreliminarId, PeriodoDeJaneiro, ProdutoDoAto(CodigoAtoQueNaoEhResultado, PapelProdutoFaseCodigo.Preliminar)),
        FaseDoCronograma(2, cenario.FaseDefinitivaId, PeriodoDeFevereiro, ProdutoDoAto(CodigoAtoQueNaoEhResultado, PapelProdutoFaseCodigo.Definitivo)),
    ];

    // A 1ª fase dá papel a um ato que o catálogo não classifica como resultado — recusa
    // que ACUMULA. A 2ª referencia um tipo de banca que não existe — resolução
    // cross-módulo, que INTERROMPE a passada levando o acumulado junto. É a combinação
    // que faz `errors[]` ser emitido: basta um item com campo para o array sair, e ele
    // é montado sobre todos.
    private static object[] FasesComRecusaAcumuladaEInterrupcao(CenarioDoCronograma cenario) =>
    [
        FaseDoCronograma(1, cenario.FasePreliminarId, PeriodoDeJaneiro, ProdutoDoAto(CodigoAtoQueNaoEhResultado, PapelProdutoFaseCodigo.Preliminar)),
        FaseDoCronograma(2, cenario.FaseDefinitivaId, PeriodoDeFevereiro, ProdutoDoAto(CodigoAtoDefinitivo, PapelProdutoFaseCodigo.Definitivo), BancaDeTipoInexistente()),
    ];

    private static void AfirmarCampoECodigoDeCadaErro(
        ErroDoProblema[] reportados, ErroDoProblema[] esperados, string motivoDosCampos = "")
    {
        reportados.Should().NotContain(e => e.Campo == null, CadaElementoDeclaraOCaminho);
        reportados.Should().Equal(esperados, $"campo e código seguem a ordem das fases do contrato. {motivoDosCampos}");
    }

    private static async Task<ErroDoProblema[]> EnviarERecusarAsync(
        CenarioDoCronograma cenario, object[] fases, string motivoDoArray = "")
    {
        using HttpResponseMessage resposta = await EnviarCronogramaAsync(cenario.Client, cenario.ProcessoId, fases);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        return await LerErrosDoProblemaAsync(resposta, motivoDoArray);
    }

    [Fact(DisplayName = "A fase de data definida na execução só com a data final é aceita no cronograma, sem início")]
    public async Task FaseDeDataNaExecucao_SoComFim_Aceita()
    {
        CascadingApiFactory api = _fixture.Factory;
        Guid faseId = await ObterFaseCanonicaSemeadaAsync(api, CodigoFaseDeDataNaExecucao);
        Guid processoId = await SemearProcessoEmRascunhoAsync(api, "PS fase só com data final", OrigemCandidatos.ImportacaoExterna);
        object[] fases = [FaseDoCronograma(1, faseId, new PeriodoDaFase(null, PeriodoDeFevereiro.Fim), [])];

        using HttpResponseMessage resposta = await EnviarCronogramaAsync(api.CreateClient(), processoId, fases);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "A publicação aceita o processo cuja fase de data definida na execução declara só a data final, e a leitura devolve a fase sem início")]
    public async Task PublicarProcessoComFaseDeDataNaExecucao_SoComFim_Aceita()
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoComFaseDeDataNaExecucaoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await PublicarAsync(client, processoId, documentoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
        CronogramaDaFaseDto fase = (await LerProcessoPelaApiAsync(client, processoId)).CronogramaFases.Single(f => f.Codigo == CodigoFaseDeDataNaExecucao).Cronograma;
        fase.Inicio.Should().BeNull("a data definida na execução não declara o início");
        fase.Fim.Should().Be(PeriodoDeFevereiro.Fim);
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComFaseDeDataNaExecucaoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = new()
        {
            Nome = $"PS publicação com fase de data na execução {Guid.CreateVersion7()}",
            FasesAdicionais = [FaseDeDataNaExecucaoSoComFim()],
        };

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);

        return (processo.Id, documento.Id);
    }

    // Não coleta inscrição nem isenção e não publica ato: sem resultado definitivo, a data final é o que fecha a fase.
    private static FaseCronograma FaseDeDataNaExecucaoSoComFim() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), CodigoFaseDeDataNaExecucao, OrigemDataFase.NaExecucao, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(Ordem: 2, Inicio: null, Fim: PeriodoDeFevereiro.Fim, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    private async Task<CenarioDoCronograma> PrepararCenarioAsync()
    {
        CascadingApiFactory api = _fixture.Factory;
        HttpClient client = api.CreateClient();

        await TiposDeAtoSeeder.SemearTiposAsync(api.Services, TiposDeAtoDoCenario);
        Guid fasePreliminarId = await ObterFaseCanonicaSemeadaAsync(api, CodigoFasePreliminar);
        Guid faseDefinitivaId = await ObterFaseCanonicaSemeadaAsync(api, CodigoFaseDefinitiva);
        Guid processoId = await SemearProcessoEmRascunhoAsync(api, "PS recusas", OrigemCandidatos.ImportacaoExterna);

        return new CenarioDoCronograma(client, processoId, fasePreliminarId, faseDefinitivaId);
    }

    private static object FaseDoCronograma(
        int ordem, Guid faseCanonicaId, PeriodoDaFase periodo, object[] produtos, object[]? bancasRequeridas = null) => new
        {
            ordem,
            faseCanonicaId,
            inicio = periodo.Inicio,
            fim = periodo.Fim,
            produtos,
            emiteParecerIndividual = false,
            bancasRequeridas = bancasRequeridas ?? [],
            regraRecurso = (object?)null,
        };

    private static object[] ProdutoDoAto(string atoCodigo, string papel) =>
        [new { atoCodigo, papel }];

    private static object[] BancaDeTipoInexistente() =>
        [new { tipoBancaId = Guid.CreateVersion7(), categoriasDocumentoIds = Array.Empty<Guid>() }];

    private sealed record PeriodoDaFase(DateTimeOffset? Inicio, DateTimeOffset Fim);

    private sealed record CenarioDoCronograma(HttpClient Client, Guid ProcessoId, Guid FasePreliminarId, Guid FaseDefinitivaId);
}
