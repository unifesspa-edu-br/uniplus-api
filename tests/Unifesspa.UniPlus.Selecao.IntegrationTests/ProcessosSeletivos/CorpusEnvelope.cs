namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Corpus da reidratação — agregados <b>ricos</b>, com ids fixos e valores
/// deliberadamente <b>não-default</b>.
/// </summary>
/// <remarks>
/// <para>
/// A golden fixture da #842 <b>não serve de oráculo</b> aqui: ela normaliza os ids
/// voláteis para tokens (<c>&lt;&lt;id-1&gt;&gt;</c>, que não é um GUID e não decodifica) e
/// o agregado dela é pobre de propósito — atendimento vazio, sem bônus, sem desempate,
/// sem eliminações. Um decoder que perdesse metade dos campos passaria por ela.
/// </para>
/// <para>
/// <b>Todo valor aqui é não-default por desenho.</b> Um decoder que esquecesse um campo
/// e o deixasse cair no default (<c>null</c>, <c>0</c>, string vazia) produziria bytes
/// distintos dos congelados — e é exatamente isso que o round-trip acusa. Um corpus de
/// valores default tornaria o esquecimento invisível.
/// </para>
/// <para>
/// Os ids são <b>fixos</b> — inclusive os das etapas, via
/// <see cref="EtapaProcesso.Reidratar"/> — porque o <c>etapa.Id</c> é o único id de
/// entidade-filha que entra no envelope (ADR-0110 D2). Os das demais filhas não entram,
/// então a volatilidade delas não vaza para os bytes.
/// </para>
/// </remarks>
internal static class CorpusEnvelope
{
    internal static readonly EnvelopeCodecV11 Codec = new();
    internal static readonly RegistroCodecsEnvelope Registro = new();

    internal const string HashDocumento = "1111111111111111111111111111111111111111111111111111111111111111";

    internal static readonly Guid AtoAbertura = new("01900000-0000-7000-8000-000000000001");
    internal static readonly Guid AtoRetificador = new("01900000-0000-7000-8000-000000000002");

    /// <summary>
    /// Ids de etapa <b>fixos</b> — é o que torna a golden fixture determinística (o
    /// <c>etapa.Id</c> é o único id de filha que entra no envelope, ADR-0110 D2).
    /// </summary>
    /// <remarks>
    /// A <paramref name="variante"/> existe só para os testes de <b>persistência</b>: eles
    /// compartilham um Postgres por classe, e dois processos com as mesmas etapas
    /// colidiriam na chave primária de <c>etapas_processo</c>. A variante 0 é a do envelope
    /// congelado — mudá-la mudaria a fixture.
    /// </remarks>
    private static Guid EtapaId(int ordem, int variante) =>
        new($"aaaa000{variante:x}-0000-4000-8000-00000000000{ordem:x}");

    private static readonly Guid OfertaMedicina = new("bbbb0000-0000-4000-8000-000000000001");
    private static readonly Guid OfertaDireito = new("bbbb0000-0000-4000-8000-000000000002");

    private static readonly Guid Documento = new("cccc0000-0000-4000-8000-000000000001");
    private static readonly Guid ReferenciaDemografica = new("dddd0000-0000-4000-8000-000000000001");

    /// <summary>Sub do publicador — evidência forense, não input de negócio.</summary>
    internal const string Ator = "corpus-tests";

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(semente, 64)).Value!;

    /// <summary>
    /// O agregado mais rico que o modelo permite: três etapas (uma de cada caráter),
    /// atendimento povoado, bônus com teto, <b>as quatro</b> variantes de desempate,
    /// classificação local com <b>três</b> regras de eliminação — duas delas do
    /// <b>mesmo</b> código — e duas ofertas de curso, uma sob a Lei 12.711 (com
    /// referência demográfica e as nove modalidades federais) e outra institucional.
    /// </summary>
    /// <remarks>
    /// A multiplicidade é deliberada: um decoder que indexasse as regras <b>por código</b>
    /// — em vez de acumulá-las numa lista — perderia a segunda <c>ELIM-NOTA-MINIMA-ETAPA</c>
    /// e o segundo <c>DESEMPATE-MAIOR-NOTA-ETAPA</c> em silêncio. Um corpus com “uma de
    /// cada variante” não pegaria isso.
    /// </remarks>
    internal static ProcessoSeletivo ProcessoRico(int variante = 0)
    {
        Guid objetiva = EtapaId(1, variante);
        Guid redacao = EtapaId(2, variante);
        Guid entrevista = EtapaId(3, variante);

        // SiSU é baseado em ENEM — é o que admite ELIM-CORTE-REDACAO e ELIM-ZERO-EM-AREA.
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS Rico 2026", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Reidratar(objetiva, "Prova Objetiva", CaraterEtapa.Ambas, peso: 3.5000m, notaMinima: 40.0000m, ordem: 1),
            EtapaProcesso.Reidratar(redacao, "Redação", CaraterEtapa.Classificatoria, peso: 2.2500m, notaMinima: null, ordem: 2),
            EtapaProcesso.Reidratar(entrevista, "Entrevista", CaraterEtapa.Eliminatoria, peso: null, notaMinima: 60.0000m, ordem: 3),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(OfertaAtendimentoEspecializado.Criar(
            condicoes: [
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000001"), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Pessoa com deficiência"),
                OfertaCondicao.Criar(new Guid("eeee0000-0000-4000-8000-000000000002"), "LACTANTE", "Lactante"),
            ],
            recursos: [
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000001"), "Ledor"),
                OfertaRecurso.Criar(new Guid("ffff0000-0000-4000-8000-000000000002"), "Prova ampliada"),
            ],
            tiposDeficiencia: [
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000001"), "Deficiência visual"),
                OfertaTipoDeficiencia.Criar(new Guid("1111aaaa-0000-4000-8000-000000000002"), "Deficiência auditiva"),
            ]).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoLei12711(), DistribuicaoInstitucional()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirBonusRegional(ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, 'b'),
            fator: 1.2000m,
            teto: 95.5000m,
            municipioConvenio: "Marabá",
            baseLegal: "Res. Unifesspa 414/2020").Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        // As QUATRO variantes de args — e DUAS do mesmo código (MAIOR-NOTA-ETAPA em
        // ordens distintas), que um decoder indexado por código colapsaria em uma.
        processo.DefinirCriteriosDesempate([
            CriterioDesempate.Criar(1, Regra(CriterioDesempateCodigo.Idoso, 'c'), new ArgsDesempateIdoso(60)).Value!,
            CriterioDesempate.Criar(2, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(objetiva)).Value!,
            CriterioDesempate.Criar(3, Regra(CriterioDesempateCodigo.MaiorNotaEtapa, 'd'), new ArgsDesempateMaiorNotaEtapa(redacao)).Value!,
            CriterioDesempate.Criar(4, Regra(CriterioDesempateCodigo.PredicadoFato, 'e'), new ArgsDesempatePredicadoFato(
                CondicaoDnf.Criar("escola_publica", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)).Value!,
            CriterioDesempate.Criar(5, Regra(CriterioDesempateCodigo.MaiorIdade, 'f'), new ArgsDesempateMaiorIdade()).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.FormulaMediaPonderada, 'a'),
            regraArredondamento: Regra(RegraArredondamentoCodigo.PrecisaoArredondarCima, '2'),
            casasArredondamento: 3,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
            nOpcoesAlocacao: 2,
            regrasEliminacao: [
                // DUAS do mesmo código, args distintos — o PS Convênios exige exatamente isso.
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(objetiva, 45.0000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, '4'), new ArgsElimNotaMinimaEtapa(redacao, 30.5000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimCorteRedacao, '5'), new ArgsElimCorteRedacao(400.0000m)).Value!,
                RegraEliminacao.Criar(Regra(RegraEliminacaoCodigo.ElimZeroEmArea, '6'), new ArgsElimZeroEmArea()).Value!,
            ]).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirCronogramaFases([FaseInscricao(), FaseResultadoPreliminarComRecurso()], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>Fase 1: coleta inscrição, sem ato produzido — a origem é InscricaoPropria.</summary>
    private static FaseCronograma FaseInscricao() => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000001"),
        codigo: "INSCRICAO",
        donoInstitucional: "CRCA",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: false,
        permiteComplementacao: true,
        produzResultado: false,
        resultadoDefinitivo: false,
        coletaInscricao: true,
        inicio: new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
        atoProduzidoCodigo: null,
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;

    /// <summary>
    /// Fase 2: agrupa as três etapas, produz o resultado preliminar (efeito irreversível),
    /// exige duas bancas e admite recurso — os DOIS pares de suspensividade exercitados: a
    /// 1ª instância com valor (5 dias corridos), a 2ª <b>nula</b> (não bloqueia — o caso
    /// normal do Ingresso via judicial). É o ramo mais rico do bloco <c>cronogramaFases</c>.
    /// </summary>
    private static FaseCronograma FaseResultadoPreliminarComRecurso() => FaseCronograma.Criar(
        ordem: 2,
        faseCanonicaOrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"),
        codigo: "RESULTADO_PRELIMINAR",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: false,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_PRELIMINAR",
        atoProduzidoEfeitoIrreversivel: true,
        bancasRequeridas: [
            BancaRequerida.Criar(new Guid("5555eeee-0000-4000-8000-000000000001"), "BANCA_ANALISE_DOCUMENTAL"),
            BancaRequerida.Criar(new Guid("5555eeee-0000-4000-8000-000000000002"), "BANCA_HETEROIDENTIFICACAO"),
        ],
        regraRecurso: RegraRecursoFase.Criar(
            Regra(RegraPrazoRecursoCodigo.AncoradoEmAto, '9'),
            new ArgsRegraPrazoRecurso(
                PrazoValor: 48.0000m,
                PrazoUnidade: UnidadePrazo.Horas,
                AtoAncoraCodigo: "RESULTADO_PRELIMINAR",
                SuspensividadePrimeiraInstanciaValor: 5.0000m,
                SuspensividadePrimeiraInstanciaUnidade: UnidadePrazo.Dias,
                SuspensividadeSegundaInstanciaValor: null,
                SuspensividadeSegundaInstanciaUnidade: null)).Value!).Value!;

    /// <summary>
    /// Oferta sob a Lei 12.711: exige a referência demográfica (INV-5) e as 8 modalidades
    /// federais + AC (INV-6). É o ramo com <c>referenciaDemografica</c> preenchida.
    /// </summary>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoLei12711()
    {
        List<ModalidadeSelecionada> modalidades =
        [
            ModalidadeSelecionada.Criar(
                new Guid("2222bbbb-0000-4000-8000-000000000001"), "AC", "Ampla concorrência",
                NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                criteriosCumulativos: [], acaoQuandoIndeferido: null,
                baseLegal: "Lei 12.711/2012 art. 1º").Value!,
        ];

        // As 8 federais. Todas cota reservada ⇒ INV-12 obriga SEGUE_CASCATA.
        (string Codigo, char Semente)[] federais =
        [
            ("LB_PPI", 'g'), ("LB_Q", 'h'), ("LB_PCD", 'i'), ("LB_EP", 'j'),
            ("LI_PPI", 'k'), ("LI_Q", 'l'), ("LI_PCD", 'm'), ("LI_EP", 'n'),
        ];

        int sufixo = 2;
        foreach ((string codigo, char _) in federais)
        {
            modalidades.Add(ModalidadeSelecionada.Criar(
                new Guid($"2222bbbb-0000-4000-8000-00000000000{sufixo:x}"),
                codigo,
                $"Reserva {codigo}",
                NaturezaLegalModalidade.CotaReservada,
                ComposicaoVagasModalidade.DentroDoVr,
                null,
                RegraRemanejamentoModalidade.SegueCascata,
                null,
                null,
                null,
                // Dois critérios, EM ORDEM — o encoder não reordena este array (é escalar,
                // sem chave de conteúdo). Um decoder que o reordenasse produziria bytes
                // distintos dos congelados (follow-up #863).
                criteriosCumulativos: codigo.StartsWith("LB", StringComparison.Ordinal)
                    ? ["renda_per_capita_ate_1sm", "egresso_escola_publica"]
                    : ["egresso_escola_publica"],
                acaoQuandoIndeferido: "RECLASSIFICAR_AC",
                baseLegal: "Lei 12.711/2012, alterada pela Lei 14.723/2023").Value!);
            sufixo++;
        }

        return ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaMedicina,
            voBase: 60,
            pr: 0.7500m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Lei12711, '7'),
            regraAjuste: Regra(RegraAjusteDistribuicaoVagasCodigo.ReconciliacaoArt11ParagrafoUnico, '0'),
            referenciaDemografica: ReferenciaReservaDemograficaSnapshot.Criar(
                ReferenciaDemografica,
                censoReferencia: "Censo IBGE 2022",
                ppiPercentual: 78.55m,
                quilombolaPercentual: 1.20m,
                pcdPercentual: 8.40m,
                baseLegal: "Lei 12.711/2012 art. 3º").Value!,
            modalidades: modalidades).Value!;
    }

    /// <summary>
    /// Oferta institucional: quadro fixo, <b>sem</b> referência demográfica — e com as
    /// variantes de remanejamento que a federal não usa (<c>DESTINO_UNICO</c> e
    /// <c>CRUZADO</c>, com par e fallback) e a composição <c>RETIRA_DE</c>.
    /// </summary>
    private static ConfiguracaoDistribuicaoVagas DistribuicaoInstitucional() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: OfertaDireito,
            voBase: 40,
            pr: 0.5000m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, '8'),
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: [
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000001"), "AC", null,
                    NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo, null,
                    RegraRemanejamentoModalidade.Nenhuma, null, null, null,
                    [], null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 15).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000002"), "V", "PcD em ampla concorrência",
                    NaturezaLegalModalidade.Suplementar, ComposicaoVagasModalidade.SuplementarAoTotal, null,
                    RegraRemanejamentoModalidade.DestinoUnico, "AC", null, null,
                    ["laudo_medico"], "RECLASSIFICAR_AC", "Lei 13.146/2015", quantidadeDeclarada: 5).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000003"), "IND", "Indígena",
                    NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "QUI", "AC",
                    ["autodeclaracao", "documento_funai"], "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
                ModalidadeSelecionada.Criar(
                    new Guid("3333cccc-0000-4000-8000-000000000004"), "QUI", "Quilombola",
                    NaturezaLegalModalidade.OutraModalidade, ComposicaoVagasModalidade.RetiraDe, "AC",
                    RegraRemanejamentoModalidade.Cruzado, null, "IND", "AC",
                    ["autodeclaracao"], "RECLASSIFICAR_REGRA_EDITAL", "Res. Unifesspa 326/2019", quantidadeDeclarada: 10).Value!,
            ]).Value!;

    internal static DadosEdital DadosRicos() => DadosEdital.Criar(
        numero: "042/2026",
        periodoInscricaoInicio: new DateOnly(2026, 3, 2),
        periodoInscricaoFim: new DateOnly(2026, 4, 15),
        documentoEditalId: Documento).Value!;

    internal static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo, RetificacaoInfo? retificacao = null, ResultadoConformidade? conformidade = null) =>
        new(processo, DadosRicos(), HashDocumento, retificacao, conformidade);

    /// <summary>
    /// Uma <see cref="VersaoConfiguracao"/> montada com <b>ids de ato fixos</b>, para que
    /// o envelope da versão N &gt; 1 — que carrega o 18º bloco com o id do ato retificado —
    /// seja determinístico. Passar por <c>Publicar</c>/<c>Retificar</c> geraria Guid v7 a
    /// partir do relógio, e a golden fixture rica não teria como existir.
    /// </summary>
    /// <remarks>
    /// O <paramref name="ato"/> é parametrizável pela mesma razão que o id da etapa: os
    /// testes de persistência compartilham um Postgres, e
    /// <c>ux_versoes_configuracao_ato_criador</c> garante que um ato cria <b>no máximo
    /// uma</b> versão. O default é o ato fixo do envelope congelado.
    /// </remarks>
    internal static VersaoConfiguracao VersaoDeAbertura(ProcessoSeletivo processo, byte[] bytes, Guid? ato = null) =>
        VersaoConfiguracao.Abrir(
            processo.Id,
            bytes,
            Codec.SchemaVersion,
            Codec.AlgoritmoHash,
            atoCriadorId: ato ?? AtoAbertura,
            atoCriadorHash: HashDocumento,
            atorUsuarioSub: Ator,
            instante: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    internal static VersaoConfiguracao VersaoDeRetificacao(VersaoConfiguracao anterior, byte[] bytes) =>
        VersaoConfiguracao.Suceder(
            anterior,
            bytes,
            Codec.SchemaVersion,
            Codec.AlgoritmoHash,
            atoCriadorId: AtoRetificador,
            atoCriadorHash: HashDocumento,
            atoCriadorRetificaId: anterior.AtoCriadorId,
            atorUsuarioSub: Ator,
            instante: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Publica o processo para que ele fique em <see cref="StatusProcesso.Publicado"/> —
    /// o estado que <see cref="ProcessoSeletivo.RestaurarConfiguracaoCongelada"/> exige,
    /// porque só um processo publicado tem versão congelada a restaurar.
    /// </summary>
    internal static void Publicar(ProcessoSeletivo processo)
    {
        SnapshotCanonico snapshot = Codec.Codificar(Entrada(processo));
        processo.Publicar(
            DadosRicos(),
            snapshot.Bytes,
            snapshot.SchemaVersion,
            snapshot.AlgoritmoHash,
            HashDocumento,
            Ator,
            TimeProvider.System).IsSuccess.Should().BeTrue();
        processo.ClearDomainEvents();
    }

    /// <summary>
    /// A sessão editorial que <b>altera os dados</b> da primeira etapa <b>preservando o
    /// Id</b> — o cenário em que a reconciliação de fato acontece (a mesma linha, dados
    /// novos). Sem ele, o teste de <c>CreatedAt</c> passaria até com uma reposição que não
    /// fizesse nada.
    /// </summary>
    internal static GrafoConfiguracao GrafoComEtapaAlterada(int variante)
    {
        ProcessoSeletivo rico = ProcessoRico(variante);
        EtapaProcesso primeira = rico.Etapas.First();

        return new GrafoConfiguracao(
            etapas: [EtapaProcesso.Reidratar(
                primeira.Id,
                "Etapa Descaracterizada",
                CaraterEtapa.Classificatoria,
                peso: 9.9999m,
                notaMinima: null,
                ordem: 7)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [DistribuicaoInstitucional()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: ConfiguracaoClassificacao.Criar(
                regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
                nOpcoesAlocacao: 1,
                regrasEliminacao: []).Value!,
            cronogramaFases: [FaseCronogramaConforme(variante)],
            documentosExigidos: [],
            referenciaTemporalFatos: null);
    }

    /// <summary>Um grafo mínimo e conforme — a "sessão editorial" que o descarte terá de desfazer.</summary>
    internal static GrafoConfiguracao GrafoPobre(int variante = 0) => new(
        etapas: [EtapaProcesso.Reidratar(new Guid($"9999fff{variante:x}-0000-4000-8000-000000000001"), "Etapa Única", CaraterEtapa.Classificatoria, 1.0000m, null, 1)],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [DistribuicaoInstitucional()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, '3'),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!,
        cronogramaFases: [FaseCronogramaConforme(variante)],
        documentosExigidos: [],
        referenciaTemporalFatos: null);

    /// <summary>Fase mínima que satisfaz o bicondicional fase×etapa (uma etapa acompanha os dois grafos acima).</summary>
    private static FaseCronograma FaseCronogramaConforme(int variante) => FaseCronograma.Criar(
        ordem: 1,
        faseCanonicaOrigemId: new Guid($"9999eee{variante:x}-0000-4000-8000-000000000001"),
        codigo: "RESULTADO_FINAL",
        donoInstitucional: "CEPS",
        origemData: OrigemDataFase.Propria,
        agrupaEtapas: true,
        permiteComplementacao: false,
        produzResultado: true,
        resultadoDefinitivo: true,
        coletaInscricao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        atoProduzidoCodigo: "RESULTADO_FINAL",
        atoProduzidoEfeitoIrreversivel: false,
        bancasRequeridas: [],
        regraRecurso: null).Value!;
}
