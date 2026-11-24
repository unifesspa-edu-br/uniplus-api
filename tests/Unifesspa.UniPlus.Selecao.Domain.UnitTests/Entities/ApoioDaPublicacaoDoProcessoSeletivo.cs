namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
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

    /// <summary>O congelamento do edital de teste, com o documento canônico de teste, trocando só o que o cenário declara.</summary>
    internal static CenarioDeCongelamento CenarioDoEdital(DadosEdital? dados = null) => new()
    {
        Dados = dados ?? NovosDados(),
        Documento = new DocumentoCongelado(BytesCanonicos, "1.0", "canonical-json/sha256@v1", HashFixo),
    };

    /// <summary>Etapa de prova objetiva com nota de 0 a 10, a mínima peça de um processo que compõe a nota.</summary>
    internal static EtapaProcesso EtapaDaProvaObjetiva(
        string nome, CaraterEtapa carater, decimal? peso, decimal? notaMinima = null, int? ordem = 1, string? faseCodigo = null) =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
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
        ProcessoSeletivo processo = ProcessoSeletivo.Criar("PS 2026 — SiSU", TiposDeProcessoDeTeste.SiSU, origemCandidatos, Guid.NewGuid(), Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        DefinirCronogramaEEtapas(processo);

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        DefinirOfertaDeAmplaConcorrencia(processo);
        DefinirClassificacaoImportada(processo);
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);
        return processo;
    }

    /// <summary>Fase mínima e conforme: agrupa etapas (há 1 acima), produz resultado e coleta inscrição (há vagas e a origem é InscricaoPropria).</summary>
    internal static FaseCronograma FaseConforme() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    /// <summary>
    /// Base legal RESOLVIDO qualquer — as fixtures abaixo testam
    /// checagens POSTERIORES ao 5º item de <see cref="ProcessoSeletivo.AvaliarConformidade"/>
    /// (B-01/CA-01), então precisam satisfazer o gate de base legal primeiro, ou nunca o
    /// alcançariam.
    /// </summary>
    internal static DocumentoExigidoBaseLegal BaseLegalResolvidaQualquer() => DocumentoExigidoBaseLegal.Criar(
        "Lei 12.711/2012, art. 3º", TipoAbrangencia.InternaEdital, StatusBaseLegal.Resolvido, null).Value!;

    private static void DefinirOfertaDeAmplaConcorrencia(ProcessoSeletivo processo)
    {
        ReferenciaRegra regraDistribuicao = ReferenciaRegra.De(
            RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1", HashFixo);
        ModalidadeSelecionada modalidade = ModalidadeDeAmplaConcorrencia();
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = regraDistribuicao, Modalidades = [modalidade] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();
    }


    private static void DefinirClassificacaoImportada(ProcessoSeletivo processo)
    {
        ReferenciaRegra regraCalculo = ReferenciaRegra.De(
            RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", HashFixo);
        ReferenciaRegra regraOrdemAlocacao = ReferenciaRegra.De(
            RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", HashFixo);
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = regraCalculo,
            RegraOrdemAlocacao = regraOrdemAlocacao,
        }).Value!;
        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
    }


    private static ModalidadeSelecionada ModalidadeDeAmplaConcorrencia()
    {
        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(ModalidadesNoCadastroDeTeste.Da("AC", NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas) with { BaseLegal = "Res. Unifesspa 532/2021" }, 40).Value!;
        return modalidade;
    }


    private static void DefinirCronogramaEEtapas(ProcessoSeletivo processo)
    {
        processo.DefinirCronogramaFases([FaseConforme()], []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaDaProvaObjetiva("Prova Objetiva", CaraterEtapa.Classificatoria, 1m, faseCodigo: "RESULTADO_FINAL"),
        ]).IsSuccess.Should().BeTrue();
    }

}
