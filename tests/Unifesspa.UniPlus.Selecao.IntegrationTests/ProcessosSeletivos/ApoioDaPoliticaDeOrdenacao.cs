namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

internal static class ApoioDaPoliticaDeOrdenacao
{
    internal static readonly SnapshotPublicacaoCanonicalizer Canonicalizador = new();

    internal static JsonArray ArrayCanonicalizado(ProcessoSeletivo processo, string caminho) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))[caminho]!.AsArray();

    internal static TipoEtapaSnapshot TipoEtapaDeProvaObjetiva() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    internal static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    internal static DadosEdital Dados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    internal static EntradaCanonicalizacao Entrada(
        ProcessoSeletivo processo,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatos = null) =>
        new(processo, Dados(), new string('0', 64), FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: metadadosFatos,
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados()));

    /// <summary>
    /// Um Guid com só o último dígito hexadecimal variando por <paramref name="sufixo"/> — as
    /// demais posições fixas garantem que <see cref="Guid.CompareTo(Guid)"/> segue a ordem numérica
    /// intuitiva do sufixo (mesmo padrão de <c>ExemploDeEnvelope.EtapaId</c>).
    /// </summary>
    internal static Guid IdFixo(int sufixo) => new($"a0000000-0000-4000-8000-00000000000{sufixo:x}");

    internal static ConfiguracaoDistribuicaoVagas DistribuicaoMinima() => ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
        new DistribuicaoDeVagasDeclarada
        {
            VagasOfertadasBase = 40,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'),
            Modalidades = [
            ModalidadeSelecionada.Criar(
                Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas, null,
                RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021", quantidadeDeclarada: 40).Value!,
        ]
        },
        new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    internal static FaseCronograma FaseMinima() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// Um processo mínimo e válido para <see cref="SnapshotPublicacaoCanonicalizer.Canonicalizar"/>,
    /// com um parâmetro por coleção sob teste — cada um substitui o default minimalista quando
    /// informado. Não publica nem valida pendências de conformidade: a canonicalização é pura
    /// (ADR-0109 D6) e cada teste chama <see cref="Canonicalizador"/> diretamente.
    /// </summary>
    internal static ProcessoSeletivo Montar(
        IReadOnlyList<EtapaProcesso>? etapas = null,
        AtendimentoEspecializado? atendimentoEspecializado = null,
        IReadOnlyList<CriterioDesempate>? criteriosDesempate = null,
        IReadOnlyList<FaseCronograma>? cronogramaFases = null,
        IReadOnlyList<RegraDeComprovacao>? documentosExigidos = null,
        IReadOnlyList<FatoColetado>? fatosColetados = null,
        IReadOnlyList<ConfiguracaoDerivacaoFato>? regrasDerivacao = null)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            "PS Política de Ordenação", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());


        ConfigurarClassificacao(processo, atendimentoEspecializado, criteriosDesempate);
        ConfigurarCronograma(processo, cronogramaFases, etapas);
        ConfigurarColeta(processo, documentosExigidos, fatosColetados, regrasDerivacao);
        return processo;
    }

    internal static ProcessoSeletivo MontarComCascata(ConfiguracaoCascataRemanejamento cascata)
    {
        ProcessoSeletivo processo = Montar();
        processo.DefinirCascataRemanejamento(cascata).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void ConfigurarClassificacao(
        ProcessoSeletivo processo, AtendimentoEspecializado? atendimentoEspecializado,
        IReadOnlyList<CriterioDesempate>? criteriosDesempate)
    {
        processo.DefinirAtendimentoEspecializado(
            atendimentoEspecializado ?? AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        processo.DefinirDistribuicaoVagas([DistribuicaoMinima()], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        if (criteriosDesempate is not null)
        {
            processo.DefinirCriteriosDesempate(criteriosDesempate).IsSuccess.Should().BeTrue();
        }

        DefinirClassificacao(processo);
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        }).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirOpcoesDeCurso(1).IsSuccess.Should().BeTrue();
    }

    private static void ConfigurarCronograma(
        ProcessoSeletivo processo, IReadOnlyList<FaseCronograma>? cronogramaFases, IReadOnlyList<EtapaProcesso>? etapas)
    {
        processo.DefinirCronogramaFases(
            cronogramaFases ?? [FaseMinima()], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas(etapas ?? [
            EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaDeProvaObjetiva(), Ordem: 1, FaseCodigo: (cronogramaFases ?? [FaseMinima()])[0].Codigo),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();
    }

    private static void ConfigurarColeta(
        ProcessoSeletivo processo, IReadOnlyList<RegraDeComprovacao>? documentosExigidos,
        IReadOnlyList<FatoColetado>? fatosColetados, IReadOnlyList<ConfiguracaoDerivacaoFato>? regrasDerivacao)
    {
        if (documentosExigidos is not null)
        {
            processo.DefinirDocumentosExigidos(documentosExigidos).IsSuccess.Should().BeTrue();
        }

        if (fatosColetados is not null)
        {
            processo.DefinirItens(fatosColetados).IsSuccess.Should().BeTrue();
        }
        DefinirRegrasDerivacao(processo, regrasDerivacao);
    }

    private static void DefinirRegrasDerivacao(ProcessoSeletivo processo, IReadOnlyList<ConfiguracaoDerivacaoFato>? regrasDerivacao)
    {
        if (regrasDerivacao is not null)
        {
            processo.DefinirRegrasDerivacao(regrasDerivacao).IsSuccess.Should().BeTrue();
        }
    }
}
