namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelope;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpusEnvelopeVagas;

internal static class CorpusEnvelopeCronograma
{
    /// <summary>
    /// O último instante das inscrições: o limite exclusivo de seis casas, que a escrita do envelope tem de
    /// preservar sem arredondar para o segundo seguinte.
    /// </summary>
    internal static readonly DateTimeOffset FimDasInscricoes = new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_990);

    internal static Guid ProdutoId(int fase, int posicao, int variante) =>
        new($"7777bbbb-000{variante:x}-4000-8000-0000000{fase:x}000{posicao:x}");

    /// <summary>
    /// Fase 1: coleta inscrição e publica um ato SEM papel — o comunicado de abertura não é
    /// resultado, e publicá-lo não torna a fase produtora.
    /// </summary>
    internal static FaseCronograma FaseInscricao(int variante = 0) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000001"),
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: true,
            ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), Fim: FimDasInscricoes, FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Reidratar(ProdutoId(1, 1, variante), "COMUNICADO_INSCRICOES", null)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    /// <summary>
    /// Fase 2: agrupa as três etapas, publica DOIS produtos preliminares — gabarito e
    /// resultado, atos distintos com o mesmo papel —, promete parecer individual, declara a
    /// fase que a conclui, exige quatro bancas — duas do MESMO tipo, separadas pelo recorte de
    /// competência, e a de análise de recursos — e admite recurso; os DOIS pares de
    /// suspensividade exercitados: a 1ª instância com valor (5 dias corridos), a 2ª
    /// <b>nula</b> (não bloqueia — o caso normal do Ingresso via judicial). É o ramo mais
    /// rico do bloco <c>cronogramaFases</c>.
    /// </summary>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA dos produtos e das bancas requeridas desta fase — nunca o conteúdo.</param>
    /// <param name="regraDeHomologacao">A regra de homologação das inscrições da fase; a do corpus quando ausente.</param>
    internal static FaseCronograma FaseResultadoPreliminarComRecurso(
        int variante = 0, bool permutar = false, RegraDeHomologacaoDasInscricoes? regraDeHomologacao = null) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000002"),
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: "RESULTADO_FINAL",
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
                Produtos: ProdutosDaFaseDeRecurso(variante, permutar), EmiteParecerIndividual: true, RegraRecurso: RegraDeRecursoDaFase(variante),
                TiposDeDecisao: [TipoMotivoDeDecisaoCodigo.Classificacao],
                RegraDeHomologacao: regraDeHomologacao ?? RegraDeHomologacaoDoCorpus()),
        bancasRequeridas: BancasRequeridasDaFaseDeRecurso(permutar));

    /// <summary>
    /// Duas alternativas, a primeira com duas condições, sem e com enquadramento, na ordem que não é a alfabética dos
    /// motivos: a escrita do envelope preserva a ordem declarada. A segunda alternativa não aponta motivo.
    /// </summary>
    internal static RegraDeHomologacaoDasInscricoes RegraDeHomologacaoDoCorpus() => RegraDeHomologacaoDasInscricoes.Declarar(
        "RESULTADO_CLASSIFICACAO",
        [
            AlternativaDeCondicoes.Declarar([
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2026, "MOTIVO_Z_SEM_RETORNO_DO_INEP", null).Value!,
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2025, "MOTIVO_A_SEM_RETORNO_DO_INEP", EnquadramentoDoCorpus()).Value!,
            ]).Value!,
            AlternativaDeCondicoes.Declarar([
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2024, null, null).Value!,
            ]).Value!,
        ]);

    private static PredicadoDnf EnquadramentoDoCorpus() => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;

    internal static IReadOnlyList<ProdutoDaFase> ProdutosDaFaseDeRecurso(int variante, bool permutar) =>
        Ordem<ProdutoDaFase>([
            ProdutoDaFase.Reidratar(ProdutoId(2, 1, variante), "GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaFase.Reidratar(ProdutoId(2, 2, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
        ], permutar);

    // DUAS bancas do MESMO tipo, distinguidas só pelo recorte de competência — o
    // arranjo que (tipoBancaOrigemId, codigo) não separa. É o que prova que a política
    // de ordenação do array desempata pelo conteúdo do item, e que o recorte
    // atravessa o round-trip byte a byte.
    internal static IReadOnlyList<BancaRequerida> BancasRequeridasDaFaseDeRecurso(bool permutar) =>
        Ordem<BancaRequerida>([
            BancaDeAnaliseDocumentalDeRenda(),
            BancaDeAnaliseDocumentalDeIdentificacao(),
            BancaDeHeteroidentificacao(),
            BancaDeAnaliseDeRecursos(),
        ], permutar);

    internal static BancaRequerida BancaDeAnaliseDocumentalDeRenda() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000001"),
            "BANCA_ANALISE_DOCUMENTAL",
            "Banca de teste",
            [CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000001"), "RENDA")],
            BancasDeTeste.Regra());

    internal static BancaRequerida BancaDeAnaliseDocumentalDeIdentificacao() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000001"),
            "BANCA_ANALISE_DOCUMENTAL",
            "Banca de teste",
            [
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000003"), "IDENTIFICACAO"),
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000002"), "ETNICO_RACIAL"),
            ],
            BancasDeTeste.Regra());

    internal static BancaRequerida BancaDeHeteroidentificacao() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000002"),
            "BANCA_HETEROIDENTIFICACAO",
            "Banca de teste",
            [],
            BancasDeTeste.Regra());

    // Quem julga o recurso da fase e o das etapas que ela abriga: o ciclo recursal do corpus só é
    // publicável com a banca de análise de recursos declarada.
    internal static BancaRequerida BancaDeAnaliseDeRecursos() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000003"),
            TipoBancaCodigo.AnaliseDeRecursos,
            "Banca de análise de recursos",
            [],
            BancasDeTeste.Regra());

    internal static RegraRecursoFase RegraDeRecursoDaFase(int variante) =>
        RegraRecursoFase.Criar(
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ParametrosRegraPrazoRecurso(
                PrazoValor: 48.0000m,
                PrazoUnidade: UnidadePrazo.Horas,
                SuspensividadePrimeiraInstanciaValor: 5.0000m,
                SuspensividadePrimeiraInstanciaUnidade: UnidadePrazo.Dias,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
            // Ancora no resultado preliminar, não no gabarito preliminar: os dois têm o
            // mesmo papel, e é a identidade do produto que diz qual publicação abre a janela.
            ProdutoId(2, 2, variante),
            ProcedimentosDoCorpusDaFase()).Value!;

    private static ProcedimentosDeRecurso ProcedimentosDoCorpusDaFase() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, Regra(RegraProcedimentoRecursoCodigo.Recalculo, '7')),
    ]);

    /// <summary>
    /// Fase 3: conclui o ciclo recursal aberto pela fase 2 — publica a definitiva de cada
    /// matéria que a fase 2 deixa aberta (o gabarito e o resultado da classificação), que é o que a
    /// declaração de concluinte da fase 2 exige que exista.
    /// </summary>
    internal static FaseCronograma FaseResultadoFinal(int variante = 0, IReadOnlyList<string?>? tiposDeDecisao = null) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000003"),
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000003"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 3, Inicio: new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 4, 10, 18, 0, 0, TimeSpan.Zero).AddTicks(1_234_560), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: ProdutosDaFaseResultadoFinal(variante),
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            TiposDeDecisao: tiposDeDecisao ?? TiposDaFaseFinalDoCorpus),
        bancasRequeridas: []);

    /// <summary>
    /// Fase sem recurso, com os tipos declarados fora de ordem: o envelope os escreve em ordem alfabética. Inclui o da isenção
    /// da taxa, que o corpus, por cobrar taxa com fundamentos de isenção, precisa que alguma fase declare para ser publicado.
    /// </summary>
    private static readonly string[] TiposDaFaseFinalDoCorpus =
    [
        TipoMotivoDeDecisaoCodigo.NotaDaEtapa, TipoMotivoDeDecisaoCodigo.IsencaoDaTaxa, TipoMotivoDeDecisaoCodigo.GabaritoDaProva, TipoMotivoDeDecisaoCodigo.AnaliseDocumental,
    ];

    internal static List<ProdutoDaFase> ProdutosDaFaseResultadoFinal(int variante) =>
    [
        ProdutoDaFase.Reidratar(ProdutoId(3, 1, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
        ProdutoDaFase.Reidratar(ProdutoId(3, 2, variante), "GABARITO", PapelProdutoFase.Definitivo),
    ];

    /// <summary>Fase mínima que satisfaz o bicondicional fase×etapa (uma etapa acompanha os dois grafos acima).</summary>
    internal static FaseCronograma FaseCronogramaConforme(int variante) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: new Guid($"9999eee{variante:x}-0000-4000-8000-000000000001"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;
}
