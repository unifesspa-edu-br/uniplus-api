namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivo;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoDadosDoEdital;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoDocumentos;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEntrada;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoEtapas;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoProcessoRico;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoVagas;

internal static class ExemploDeProcessoSeletivoCronograma
{
    /// <summary>
    /// O último instante das inscrições: o limite exclusivo de seis casas, que a escrita do envelope tem de
    /// preservar sem arredondar para o segundo seguinte.
    /// </summary>
    internal static readonly DateTimeOffset FimDasInscricoes = new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_990);

    // O id da banca também leva a variante: processos de testes de persistência diferentes não podem repetir a chave no mesmo Postgres.
    internal static Guid BancaId(int numero, int variante) =>
        new($"bbbb0000-000{variante:x}-4000-8000-00000000000{numero:x}");

    internal static Guid ProdutoId(int fase, int posicao, int variante) =>
        new($"7777bbbb-000{variante:x}-4000-8000-0000000{fase:x}000{posicao:x}");

    /// <summary>
    /// Fase 1: coleta inscrição e publica um ato SEM papel — o comunicado de abertura não é
    /// resultado, e publicá-lo não torna a fase produtora.
    /// </summary>
    internal static FaseCronograma FaseInscricao(int variante = 0) => FaseCronograma.Reidratar(ChaveLocal.Criar("fase_inscricao").Value!,
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000001"),
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: true,
            ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero), Fim: FimDasInscricoes, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Reidratar(ProdutoId(1, 1, variante), "COMUNICADO_INSCRICOES", null)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    /// <summary>
    /// Fase 2: agrupa as três etapas, publica DOIS produtos preliminares — gabarito e
    /// resultado, atos distintos com o mesmo papel — e o definitivo de cada um, promete parecer
    /// individual, exige quatro bancas — duas do MESMO tipo, separadas pelos candidatos atendidos pela banca, de
    /// competência, e a de análise de recursos — e admite recurso, sem instância superior,
    /// com a unidade decidindo o efeito suspensivo em cada recurso e sem guardar a vaga. É o ramo mais rico do bloco <c>cronogramaFases</c>.
    /// </summary>
    /// <param name="variante">Ver <see cref="EtapaId"/> — só distingue processos no mesmo Postgres entre testes de persistência.</param>
    /// <param name="permutar">Inverte a ordem de ENTRADA dos produtos e das bancas requeridas desta fase — nunca o conteúdo.</param>
    /// <param name="regraDeHomologacao">A regra de homologação das inscrições da fase; a do exemplo quando ausente.</param>
    /// <param name="comBancasExigidasNaPublicacao">Opt-in: acrescenta as bancas que a publicação exige e que a golden fixture não tem (homologação das inscrições e 2ª instância).</param>
    internal static FaseCronograma FaseResultadoPreliminarComRecurso(
        int variante = 0, bool permutar = false, RegraDeHomologacaoDasInscricoes? regraDeHomologacao = null, bool comBancasExigidasNaPublicacao = false) => FaseCronograma.Reidratar(ChaveLocal.Criar("fase_resultado_preliminar").Value!,
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000002"),
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"), Codigo: "RESULTADO_PRELIMINAR", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps)
        { NoMesmoMomentoDaAnterior = true },
        new PublicacoesDaFase(
                Produtos: ProdutosDaFaseDeRecurso(variante, permutar), EmiteParecerIndividual: true, RegraRecurso: RegraDeRecursoDaFase(variante),
                TiposDeDecisao: [TipoMotivoDeDecisao.Classificacao.Codigo(), TipoMotivoDeDecisao.HomologacaoDasInscricoes.Codigo()],
                RegraDeHomologacao: regraDeHomologacao ?? RegraDeHomologacaoDoExemplo()),
        bancasRequeridas: BancasRequeridasDaFaseDeRecurso(permutar, comBancasExigidasNaPublicacao, variante));

    /// <summary>
    /// Duas alternativas, a primeira com duas condições, sem e com enquadramento, na ordem que não é a alfabética dos
    /// motivos: a escrita do envelope preserva a ordem declarada. Cada condição aponta o motivo desfavorável que a publicação confere.
    /// </summary>
    internal static RegraDeHomologacaoDasInscricoes RegraDeHomologacaoDoExemplo() => RegraDeHomologacaoDasInscricoes.Declarar(
        "RESULTADO_CLASSIFICACAO",
        [
            AlternativaDeCondicoes.Declarar([
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2026, "MOTIVO_Z_SEM_RETORNO_DO_INEP", null).Value!,
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2025, "MOTIVO_A_SEM_RETORNO_DO_INEP", EnquadramentoDoExemplo()).Value!,
            ]).Value!,
            AlternativaDeCondicoes.Declarar([
                CondicaoDeHomologacao.Declarar("RESULTADO_NO_RETORNO_DO_INEP", 2024, "MOTIVO_M_SEM_RETORNO_DO_INEP", null).Value!,
            ]).Value!,
        ]);

    private static PredicadoDnf EnquadramentoDoExemplo() => PredicadoDnf.CriarDeCondicoesAgrupadas(
        [(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!;

    internal static IReadOnlyList<ProdutoDaFase> ProdutosDaFaseDeRecurso(int variante, bool permutar) =>
        Ordem<ProdutoDaFase>([
            ProdutoDaFase.Reidratar(ProdutoId(2, 1, variante), "GABARITO", PapelDaPublicacaoPrevista.Preliminar),
            ProdutoDaFase.Reidratar(ProdutoId(2, 2, variante), "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
            ProdutoDaFase.Reidratar(ProdutoId(2, 3, variante), "GABARITO", PapelDaPublicacaoPrevista.Definitivo),
            ProdutoDaFase.Reidratar(ProdutoId(2, 4, variante), "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo),
        ], permutar);

    // DUAS bancas do MESMO tipo, distinguidas pelo id, pela posição no tipo e pelos candidatos
    // atendidos. A posição vem da ordem de chegada dentro do tipo, então a permutação da entrada
    // move as duas juntas e preserva a ordem relativa entre elas: inverter uma em relação à outra
    // é mudar a configuração, não só a ordem de entrada.
    internal static IReadOnlyList<BancaRequerida> BancasRequeridasDaFaseDeRecurso(bool permutar, bool comBancasExigidasNaPublicacao = false, int variante = 0)
    {
        BancaRequerida[] daGolden = permutar
            ? [BancaDeAnaliseDeRecursos(variante), BancaDeHeteroidentificacao(variante), BancaDeAnaliseDocumentalDeRenda(variante), BancaDeAnaliseDocumentalDeIdentificacao(variante)]
            : [BancaDeAnaliseDocumentalDeRenda(variante), BancaDeAnaliseDocumentalDeIdentificacao(variante), BancaDeHeteroidentificacao(variante), BancaDeAnaliseDeRecursos(variante)];

        return comBancasExigidasNaPublicacao ? [.. daGolden, BancaDeHomologacaoDasInscricoes(variante), BancaDeAnaliseDeRecursosDeSegundaInstancia(variante)] : daGolden;
    }

    // A banca que julga a homologação das inscrições, que a publicação exige de toda fase que homologa.
    internal static BancaRequerida BancaDeHomologacaoDasInscricoes(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_5").Value,
            BancaId(5, variante),
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.HomologacaoDasInscricoes, new Guid("5555eeee-0000-4000-8000-000000000004")),
            [],
            BancasDeTeste.Regra());

    internal static BancaRequerida BancaDeAnaliseDocumentalDeRenda(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_1").Value,
            BancaId(1, variante),
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, new Guid("5555eeee-0000-4000-8000-000000000001")),
            [CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000001"), "RENDA", "RENDA")],
            BancasDeTeste.Regra());

    internal static BancaRequerida BancaDeAnaliseDocumentalDeIdentificacao(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_2").Value,
            BancaId(2, variante),
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, new Guid("5555eeee-0000-4000-8000-000000000001")),
            [
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000003"), "IDENTIFICACAO", "IDENTIFICACAO"),
                CategoriaJulgada.Criar(new Guid("7777ffff-0000-4000-8000-000000000002"), "ETNICO_RACIAL", "ETNICO_RACIAL"),
            ],
            BancasDeTeste.Regra());

    internal static BancaRequerida BancaDeHeteroidentificacao(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_3").Value,
            BancaId(3, variante),
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Heteroidentificacao, new Guid("5555eeee-0000-4000-8000-000000000002")),
            [],
            BancasDeTeste.Regra());

    // Quem julga o recurso da fase e o das etapas que ela abriga: o ciclo recursal do exemplo só é
    // publicável com a banca de análise de recursos declarada.
    internal static BancaRequerida BancaDeAnaliseDeRecursos(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_4").Value,
            BancaId(4, variante),
            TiposDeBancaCopiadosDeTeste.DeRecursos(new Guid("5555eeee-0000-4000-8000-000000000003")),
            [],
            BancasDeTeste.Regra());

    // Quem julga em 2ª instância os recursos das etapas da fase que admitem instância superior: a publicação exige essa banca.
    internal static BancaRequerida BancaDeAnaliseDeRecursosDeSegundaInstancia(int variante = 0) =>
        BancaRequerida.ReconstruirDosDadosSalvos(ChaveLocal.Criar("banca_fase_6").Value,
            BancaId(6, variante),
            TiposDeBancaCopiadosDeTeste.DeSegundaInstancia(new Guid("5555eeee-0000-4000-8000-000000000005")),
            [],
            BancasDeTeste.Regra());

    internal static RegraRecursoFase RegraDeRecursoDaFase(int variante) =>
        RegraRecursoFase.Criar(
            Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), '9'),
            ParametrosDoRecursoDeTeste.Declarados(
                48m, UnidadePrazo.Horas, efeitoSuspensivo: "UNIDADE_DECIDE_EM_CADA_RECURSO", guardaDaVaga: "NAO_GUARDA"),
            // Ancora no resultado preliminar, não no gabarito preliminar: os dois têm o
            // mesmo papel, e é a identidade do produto que diz qual publicação abre a janela.
            ProdutoId(2, 2, variante),
            ProcedimentosDoExemploDaFase()).Value!;

    private static ProcedimentosDeRecurso ProcedimentosDoExemploDaFase() => ProcedimentosDeRecurso.De(
    [
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, Regra(RegraDeProcedimentoDoRecurso.Recalculo.Codigo(), '7')),
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, Regra(RegraDeProcedimentoDoRecurso.Recorrecao.Codigo(), '8')),
    ]);

    /// <summary>
    /// Fase 3: não publica ato; tem a data final declarada.
    /// </summary>
    internal static FaseCronograma FaseResultadoFinal(int variante = 0, IReadOnlyList<string?>? tiposDeDecisao = null) => FaseCronograma.Reidratar(ChaveLocal.Criar("fase_resultado_final").Value!,
        id: new Guid($"6666aaaa-000{variante:x}-4000-8000-000000000003"),
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000003"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 3, Inicio: new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 4, 10, 18, 0, 0, TimeSpan.Zero).AddTicks(1_234_560), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            Produtos: [],
            EmiteParecerIndividual: false,
            RegraRecurso: null,
            TiposDeDecisao: tiposDeDecisao ?? TiposDaFaseFinalDoExemplo),
        bancasRequeridas: []);

    /// <summary>
    /// Fase sem recurso, com os tipos declarados fora de ordem: o envelope os escreve em ordem alfabética. Inclui o da isenção
    /// da taxa, que o exemplo, por cobrar taxa com fundamentos de isenção, precisa que alguma fase declare para ser publicado.
    /// </summary>
    private static readonly string[] TiposDaFaseFinalDoExemplo =
    [
        TipoMotivoDeDecisao.NotaDaEtapa.Codigo(), TipoMotivoDeDecisao.IsencaoDaTaxa.Codigo(), TipoMotivoDeDecisao.GabaritoDaProva.Codigo(), TipoMotivoDeDecisao.AnaliseDocumental.Codigo(),
    ];

    /// <summary>Fase mínima que satisfaz o bicondicional fase×etapa (uma etapa acompanha os dois grafos acima).</summary>
    internal static FaseCronograma FaseCronogramaConforme(int variante) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            OrigemId: new Guid($"9999eee{variante:x}-0000-4000-8000-000000000001"), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false,
            ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: []).Value!;
}
