namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
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
            "PS Exigência Duplicada", TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
            LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!,
            identificadorLegivel: IdentificadoresDeTeste.Novo());

    private static void DefinirOfertaEDistribuicao(ProcessoSeletivo processo)
    {
        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirDistribuicaoVagas([DistribuicaoDeUmaModalidadeAmpla()], FatosDeModalidadeDeTeste.DoCatalogo, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private static ConfiguracaoDistribuicaoVagas DistribuicaoDeUmaModalidadeAmpla() =>
        ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(), voBase: 40, pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null, referenciaDemografica: null,
            modalidades: [ModalidadeAmpla()], ofertaCurso: OfertaCursoDeTeste.Medicina).Value!;

    private static ModalidadeSelecionada ModalidadeAmpla() =>
        ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.ResidualDoVo,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;

    private static void DefinirClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!,
            regraArredondamento: null, casasArredondamento: null,
            regraOrdemAlocacao: ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('c', 64)).Value!,
            regrasEliminacao: [], baseadoEmEnem: false, resolucaoPesoAreaEnem: null, quadroPesoAreaEnem: [], escalaNotaFinal: null).Value!;

        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma DefinirCronograma(ProcessoSeletivo processo)
    {
        FaseCronograma fase = FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "INSCRICAO", OrigemDataFase.NoEdital,
            agrupaEtapas: true, permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false, inicio: InicioDaFase,
            fim: FimDaFase, produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false,
            bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([fase], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return fase;
    }

    private static void DefinirEtapasFormularioETaxa(ProcessoSeletivo processo)
    {
        processo.DefinirEtapas([ProvaObjetiva()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormularioDeInscricaoPropria(processo);

        // Publicar sem declarar cobrança de taxa é recusado.
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);
    }

    private static EtapaProcesso ProvaObjetiva() =>
        EtapaProcesso.Criar(
            "Prova Objetiva", CaraterEtapa.Classificatoria,
            TipoEtapaSnapshot.Criar(new Guid("019fee1e-7000-7000-8000-000000000001"), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
            peso: 1m, ordem: 1, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "INSCRICAO").Value!;

    private static void DefinirDuasExigenciasDe(ProcessoSeletivo processo, FaseCronograma fase)
    {
        DocumentoExigido rg = ExigenciaDeInscricao(fase, "RG", "Documento de identidade");
        DocumentoExigido cpf = ExigenciaDeInscricao(fase, "CPF", "CPF");

        processo.DefinirDocumentosExigidos(
            [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido ExigenciaDeInscricao(FaseCronograma fase, string codigo, string nome) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), codigo, nome, "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(true, null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;

    private static DadosEdital DadosDoEdital() =>
        DadosEdital.Criar(
            "088/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;

    private static SnapshotCanonico Congelar(ProcessoSeletivo processo, DadosEdital dados) =>
        new SnapshotPublicacaoCanonicalizer().Canonicalizar(
            new EntradaCanonicalizacao(processo, dados, HashDocumento, FusoInstitucional.ZoneId, ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(), TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    private static VersaoConfiguracao Publicar(ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, byte[] bytes)
    {
        Result<VersaoConfiguracao> publicacao = processo.Publicar(
            dados, bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, HashDocumento, "testes", TimeProvider.System, CorpusEnvelope.ContextoRico(), FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
        publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);

        return publicacao.Value!;
    }
}
