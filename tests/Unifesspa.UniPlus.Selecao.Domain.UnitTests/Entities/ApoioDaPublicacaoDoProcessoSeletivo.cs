namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDaPublicacaoDoProcessoSeletivo
{
    internal static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    internal static readonly byte[] BytesCanonicos = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    internal static DadosEdital NovosDados() => DadosEdital.Criar(
        numero: "001/2026",
        periodoInscricaoInicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
        periodoInscricaoFim: new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
        documentoEditalId: Guid.CreateVersion7()).Value!;

    /// <summary>Etapa de prova objetiva com nota de 0 a 10, a mínima peça de um processo que compõe a nota.</summary>
    internal static EtapaProcesso EtapaDaProvaObjetiva(
        string nome, CaraterEtapa carater, decimal? peso, decimal? notaMinima = null, int? ordem = 1, string? faseCodigo = null) =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(
                nome, carater, TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                ordem, faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(
                peso, notaMinima, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    /// <summary>
    /// Monta um processo minimamente conforme (CA-07: etapas, oferta de
    /// atendimento, distribuição de vagas, classificação) — o par exigido
    /// pelo gate de conformidade de <see cref="ProcessoSeletivo.Publicar"/>.
    /// </summary>
    internal static ProcessoSeletivo NovoProcessoConforme(
        OrigemCandidatos origemCandidatos = OrigemCandidatos.InscricaoPropria)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, origemCandidatos, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases([FaseConforme()], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaDaProvaObjetiva("Prova Objetiva", CaraterEtapa.Classificatoria, 1m, faseCodigo: "RESULTADO_FINAL"),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDistribuicaoVagasCodigo.Institucional, "v1", HashFixo);
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            regraDeAplicabilidade: RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla),
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ReferenciaRegra regraCalculo = ReferenciaRegra.De(
            RegraCalculoCodigo.ClassificacaoImportada, "v1", HashFixo);
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.De(
            RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", HashFixo);
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: regraCalculo,
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: regraOrdemAlocacao,
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();


        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>Fase mínima e conforme: agrupa etapas (há 1 acima), produz resultado e coleta inscrição (há vagas e a origem é InscricaoPropria).</summary>
    internal static FaseCronograma FaseConforme() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    /// <summary>
    /// Base legal RESOLVIDO qualquer (Story #554, PR #898) — as fixtures abaixo testam
    /// checagens POSTERIORES ao 5º item de <see cref="ProcessoSeletivo.AvaliarConformidade"/>
    /// (B-01/CA-01), então precisam satisfazer o gate de base legal primeiro, ou nunca o
    /// alcançariam.
    /// </summary>
    internal static DocumentoExigidoBaseLegal BaseLegalResolvidaQualquer() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;
}
