namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um processo publicável com duas exigências documentais de inscrição, e a publicação dele sobre um
/// envelope adulterado: é o cenário que precisa de um encoder real produzindo o bloco
/// <c>documentosExigidos</c>, que o processo rico do corpus não popula.
/// </summary>
internal static class ProcessoComDuasExigenciasDeTeste
{
    private const string HashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";

    private static readonly DateTimeOffset InicioDaFase = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset FimDaFase = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Congela o processo, adultera o envelope congelado (que precisa mudar os bytes) e publica o processo
    /// sobre os bytes adulterados — o hash da versão passa a ser o dos bytes que o leitor receberá.
    /// </summary>
    public static VersaoConfiguracao PublicarComEnvelopeAdulterado(Action<JsonObject> adulterar)
    {
        ProcessoSeletivo processo = Montar();
        DadosEdital dados = DadosDoEdital();
        SnapshotCanonico congelado = Congelar(processo, dados);

        JsonObject envelope = VersoesDoEnvelopeDeTeste.EnvelopeDosBytes(congelado.Bytes);
        adulterar(envelope);

        byte[] adulterados = PerfilCanonicoV1.Instancia.Serializar(envelope);
        adulterados.Should().NotEqual(congelado.Bytes, "pré-condição: a adulteração tem de mudar os bytes");

        return Publicar(processo, dados, congelado, adulterados);
    }

    private static ProcessoSeletivo Montar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirOfertaEDistribuicao(processo);
        DefinirClassificacao(processo);
        FaseCronograma fase = DefinirCronograma(processo);
        DefinirEtapasFormularioETaxa(processo);
        DefinirDuasExigenciasDe(processo, fase);

        return processo;
    }

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar(
            "PS Exigência Duplicada", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static void DefinirOfertaEDistribuicao(ProcessoSeletivo processo)
    {
        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoDeUmaModalidadeAmpla()], FatosDeModalidadeDeTeste.DoCatalogo)
            .IsSuccess.Should().BeTrue();
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDeUmaModalidadeAmpla() =>
        ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
            new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = ReferenciaRegra.De(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)), Modalidades = [ModalidadeAmpla()] },
            new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;

    private static ModalidadeSelecionada ModalidadeAmpla() =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)),
        }).Value!;

        processo.DefinirClassificacao(classificacao).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma DefinirCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(1, Inicio: InicioDaFase, Fim: FimDaFase, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(resultado: "INSCRICAO"),
            bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;
        processo.DefinirCronogramaFases([fase], []).IsSuccess.Should().BeTrue();

        return fase;
    }

    private static void DefinirEtapasFormularioETaxa(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas([ProvaObjetiva()]).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        // Publicar sem declarar cobrança de taxa é recusado.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static EtapaProcesso ProvaObjetiva() =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "INSCRICAO"),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static void DefinirDuasExigenciasDe(ProcessoSeletivo processo, FaseCronograma fase)
    {
        DocumentoExigido rg = ExigenciaDeInscricao(fase, "RG", "Documento de identidade");
        DocumentoExigido cpf = ExigenciaDeInscricao(fase, "CPF", "CPF");

        processo.DefinirDocumentosExigidos(
            [RegraDeComprovacao.CriarRegraDeDocumento(rg, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(cpf, 1).Value!])
            .IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaDeInscricao(FaseCronograma fase, string codigo, string nome) =>
        DocumentoExigido.Criar(
            new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), codigo, nome, "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(false, null, []),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null).Value!;

    private static DadosEdital DadosDoEdital() =>
        DadosEdital.Criar(
            "088/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static SnapshotCanonico Congelar(ProcessoSeletivo processo, DadosEdital dados) =>
        new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, HashDocumento, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, byte[] bytes)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, HashDocumento), AtorUsuarioContaId = "testes", Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        return publicacao.Value!;
    }
}
