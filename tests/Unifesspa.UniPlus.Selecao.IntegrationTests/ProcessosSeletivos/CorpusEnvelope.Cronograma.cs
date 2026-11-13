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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static partial class CorpusEnvelope
{
    private static Guid ProdutoId(int fase, int posicao, int variante) =>
        new($"7777bbbb-000{variante:x}-4000-8000-0000000{fase:x}000{posicao:x}");

    /// <summary>
    /// Fase 1: coleta inscrição e publica um ato SEM papel — o comunicado de abertura não é
    /// resultado, e publicá-lo não torna a fase produtora.
    /// </summary>
    private static FaseCronograma FaseInscricao(int variante = 0) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000001"),
        ordem: 1,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"),
        codigo: "INSCRICAO",
        origemData: OrigemDataFase.NoEdital,
        agrupaEtapas: false,
        permiteComplementacao: true,
        coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Reidratar(ProdutoId(1, 1, variante), "COMUNICADO_INSCRICOES", null)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null,
        unidadeResponsavel: UnidadesDeTeste.Ceps);

    /// <summary>
    /// Fase 2: agrupa as três etapas, publica DOIS produtos preliminares — gabarito e
    /// resultado, atos distintos com o mesmo papel —, promete parecer individual, declara a
    /// fase que a conclui, exige três bancas — duas do MESMO tipo, separadas pelo recorte de
    /// competência — e admite recurso; os DOIS pares de
    /// suspensividade exercitados: a 1ª instância com valor (5 dias corridos), a 2ª
    /// <b>nula</b> (não bloqueia — o caso normal do Ingresso via judicial). É o ramo mais
    /// rico do bloco <c>cronogramaFases</c>.
    /// </summary>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA dos produtos e das bancas requeridas desta fase — nunca o conteúdo.</param>
    private static FaseCronograma FaseResultadoPreliminarComRecurso(int variante = 0, bool permutar = false) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000002"), ordem: 2,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"), codigo: "RESULTADO_PRELIMINAR",
        origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
        produtos: ProdutosDaFaseDeRecurso(variante, permutar),
        faseConcluinteCodigo: "RESULTADO_FINAL", emiteParecerIndividual: true,
        bancasRequeridas: BancasRequeridasDaFaseDeRecurso(permutar), regraRecurso: RegraDeRecursoDaFase(variante),
        unidadeResponsavel: UnidadesDeTeste.Ceps);

    private static IReadOnlyList<ProdutoDaFase> ProdutosDaFaseDeRecurso(int variante, bool permutar) =>
        Ordem<ProdutoDaFase>([
            ProdutoDaFase.Reidratar(ProdutoId(2, 1, variante), "GABARITO", PapelProdutoFase.Preliminar),
            ProdutoDaFase.Reidratar(ProdutoId(2, 2, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
        ], permutar);

    // DUAS bancas do MESMO tipo, distinguidas só pelo recorte de competência — o
    // arranjo que (tipoBancaOrigemId, codigo) não separa. É o que prova que a política
    // de ordenação do array desempata pelo conteúdo do item, e que o recorte
    // atravessa o round-trip byte a byte.
    private static IReadOnlyList<BancaRequerida> BancasRequeridasDaFaseDeRecurso(bool permutar) =>
        Ordem<BancaRequerida>([
            BancaDeAnaliseDocumentalDeRenda(),
            BancaDeAnaliseDocumentalDeIdentificacao(),
            BancaRequerida.Criar(
                new Guid("5555eeee-0000-4000-8000-000000000002"),
                "BANCA_HETEROIDENTIFICACAO",
                []),
        ], permutar);

    private static BancaRequerida BancaDeAnaliseDocumentalDeRenda() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000001"),
            "BANCA_ANALISE_DOCUMENTAL",
            [CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000001"), "RENDA")]);

    private static BancaRequerida BancaDeAnaliseDocumentalDeIdentificacao() =>
        BancaRequerida.Criar(
            new Guid("5555eeee-0000-4000-8000-000000000001"),
            "BANCA_ANALISE_DOCUMENTAL",
            [
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000003"), "IDENTIFICACAO"),
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000002"), "ETNICO_RACIAL"),
            ]);

    private static RegraRecursoFase RegraDeRecursoDaFase(int variante) =>
        RegraRecursoFase.Criar(
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ArgsRegraPrazoRecurso(
                PrazoValor: 48.0000m,
                PrazoUnidade: UnidadePrazo.Horas,
                SuspensividadePrimeiraInstanciaValor: 5.0000m,
                SuspensividadePrimeiraInstanciaUnidade: UnidadePrazo.Dias,
                AdmiteInstanciaSuperior: false,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null),
            // Ancora no resultado preliminar, não no gabarito preliminar: os dois têm o
            // mesmo papel, e é a identidade do produto que diz qual publicação abre a janela.
            ProdutoId(2, 2, variante)).Value!;

    /// <summary>
    /// Fase 3: conclui o ciclo recursal aberto pela fase 2 — publica a definitiva de cada
    /// matéria que a fase 2 deixa aberta (o gabarito e o resultado da classificação), que é o que a
    /// declaração de concluinte da fase 2 exige que exista.
    /// </summary>
    private static FaseCronograma FaseResultadoFinal(int variante = 0) => FaseCronograma.Reidratar(
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000003"),
        ordem: 3,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000003"),
        codigo: "RESULTADO_FINAL",
        origemData: OrigemDataFase.NoEdital,
        agrupaEtapas: false,
        permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 4, 10, 18, 0, 0, TimeSpan.Zero),
        produtos:
        [
            ProdutoDaFase.Reidratar(ProdutoId(3, 1, variante), "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ProdutoDaFase.Reidratar(ProdutoId(3, 2, variante), "GABARITO", PapelProdutoFase.Definitivo),
        ],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null,
        unidadeResponsavel: UnidadesDeTeste.Ceps);

    /// <summary>Fase mínima que satisfaz o bicondicional fase×etapa (uma etapa acompanha os dois grafos acima).</summary>
    private static FaseCronograma FaseCronogramaConforme(int variante) => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid($"9999eee{variante:x}-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        origemData: OrigemDataFase.NoEdital,
        agrupaEtapas: true,
        permiteComplementacao: false,
        coletaInscricao: false, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: [],
        regraRecurso: null,
        unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
}
