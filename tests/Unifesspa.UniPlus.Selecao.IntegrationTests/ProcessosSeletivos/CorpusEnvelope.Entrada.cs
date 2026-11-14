namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
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
    internal static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        RetificacaoInfo? retificacao = null,
        ResultadoConformidade? conformidade = null,
        bool permutarValoresSelecionaveis = false) =>
        new(
            processo, DadosRicos(), HashDocumento, FusoInstitucional.ZoneId, retificacao, conformidade,
            ValoresSelecionaveisCongelados: ValoresSelecionaveisRicos(permutarValoresSelecionaveis),
            CalendarioDiasUteis: CalendarioRico(),
            AgregadosDosGrupos: AgregadosRicos,
            ApresentacaoDosPressupostos: ApresentacaoRica,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    /// <summary>
    /// A apresentação de dois derivados do sistema: o município, que o domínio categórico do Geo dá como
    /// campo de município, e a faixa etária, numérica.
    /// </summary>
    internal static IReadOnlyDictionary<string, ApresentacaoDoFato> ApresentacaoRica { get; } =
        new Dictionary<string, ApresentacaoDoFato>(StringComparer.Ordinal)
        {
            ["MUNICIPIO_RESIDENCIA"] = ApresentacaoDoFato.DoCatalogo(
                "MUNICIPIO_RESIDENCIA", "Município de residência", DominioDoCatalogo.Categorico, DominioDoCatalogo.Escalar, DominioDoCatalogo.FonteGeoMunicipio),
            ["FAIXA_ETARIA"] = ApresentacaoDoFato.DoCatalogo(
                "FAIXA_ETARIA", "Faixa etária", DominioDoCatalogo.Numerico, DominioDoCatalogo.Escalar, null),
        };

    /// <summary>
    /// Os agregados sobre a composição familiar do corpus rico (ADR-0138): um de cada operação, para
    /// o envelope congelar o fato de membro e a operação de cada um junto do grupo.
    /// </summary>
    internal static IReadOnlyList<DefinicaoAgregado> AgregadosRicos { get; } =
    [
        new("RURAL_NA_FAMILIA", "COMPOSICAO_FAMILIAR", "TRABALHADOR_RURAL", OperacaoAgregado.Existe),
        new("PARENTESCOS_NA_FAMILIA", "COMPOSICAO_FAMILIAR", CandidatoComoMembro.FatoParentesco, OperacaoAgregado.ValoresPresentes),
    ];

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
    internal static void Publicar(ProcessoSeletivo processo, EntradaCanonicalizacao? entrada = null)
    {
        SnapshotCanonico snapshot = Codec.Codificar(entrada ?? Entrada(processo));
        processo.Publicar(
            DadosRicos(),
            snapshot.Bytes,
            snapshot.SchemaVersion,
            snapshot.AlgoritmoHash,
            HashDocumento,
            Ator,
            TimeProvider.System, ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum).IsSuccess.Should().BeTrue();
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
                TipoEtapaProvaObjetiva(),
                peso: 9.9999m,
                notaMinima: null,
                ordem: 7, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
            ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
            distribuicaoVagas: [DistribuicaoInstitucional()],
            bonusRegional: null,
            criteriosDesempate: [],
            classificacao: ConfiguracaoClassificacao.Criar(
                regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
                regraArredondamento: null,
                casasArredondamento: null,
                regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '3'),
                regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
            cronogramaFases: [FaseCronogramaConforme(variante)],
            documentosExigidos: [],
            nosExigencia: [],
            referenciaTemporalFatos: null,
            fatosColetados: [],
            regrasDerivacao: [],
            numeroDeOpcoesDeCurso: 1);
    }

    /// <summary>Um grafo mínimo e conforme — a "sessão editorial" que o descarte terá de desfazer.</summary>
    internal static GrafoConfiguracao GrafoPobre(int variante = 0) => new(
        etapas: [EtapaProcesso.Reidratar(new Guid($"9999fff{variante:x}-0000-4000-8000-000000000001"), "Etapa Única", CaraterEtapa.Classificatoria, TipoEtapaProvaObjetiva(), 1.0000m, null, 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva)],
        ofertaAtendimento: OfertaAtendimentoEspecializado.Criar([], [], []).Value!,
        distribuicaoVagas: [DistribuicaoInstitucional()],
        bonusRegional: null,
        criteriosDesempate: [],
        classificacao: ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, 'a'),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, '3'),
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!,
        cronogramaFases: [FaseCronogramaConforme(variante)],
        documentosExigidos: [],
        nosExigencia: [],
        referenciaTemporalFatos: null,
        fatosColetados: [],
        regrasDerivacao: [],
        numeroDeOpcoesDeCurso: 1);


    /// <summary>Um termo exigido com versão fixa, para envelope determinístico.</summary>
    internal static TermoExigidoFormulario Termo(
        string codigo, int ordem, string formaAceite = "REGISTRO_DIGITAL_SEM_LOG_IP", PredicadoDnf? exibicao = null, Obrigatoriedade? obrigatoriedade = null) =>
        TermoExigidoFormulario.Criar(
            codigo,
            ordem,
            new VersaoTermoEscolhida(
                Guid.Parse("0199a000-0000-7000-8000-00000000a001"), Guid.Parse("0199a000-0000-7000-8000-00000000b001"),
                "Declaração de veracidade", "Declaro que as informações prestadas são verdadeiras.", "Lei 9.784/1999, art. 4º",
                formaAceite, new string('a', 64)),
            exibicao,
            obrigatoriedade ?? Obrigatoriedade.Sempre).Value!;
}
