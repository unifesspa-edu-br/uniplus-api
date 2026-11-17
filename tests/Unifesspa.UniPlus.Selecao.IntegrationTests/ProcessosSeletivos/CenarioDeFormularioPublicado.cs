namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem compartilhada dos testes do formulário público lido do banco (Postgres real): o processo
/// com um fato de seleção, a publicação com a divulgação semeada e as leituras do formulário publicado
/// e do rascunho.
/// </summary>
internal sealed class CenarioDeFormularioPublicado
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeFormularioPublicado(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    // O registro de PRODUÇÃO (não um substituto de teste): a leitura pública real, com
    // Postgres real, tem de passar pelo mesmo gate de versão que o handler usa fora dos
    // testes — reconhecendo hoje somente o codec vivo (ADR-0110 Emenda 2).
    public static RegistroCodecsEnvelope RegistroCodecs { get; } = new RegistroCodecsEnvelope();

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public static SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada) => Canonicalizer.Canonicalizar(entrada);

    public static DadosEdital DadosDoEdital(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), documentoId).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "RESULTADO_FINAL", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: false, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), FaseConcluinteCodigo: null,
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    public static ProcessoSeletivo NovoProcessoComFatoDeSelecao(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            nome, TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.UnidadeAdministradoraSnapshot.Criar(
                "CEPS",
                "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!, LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, identificadorLegivel: IdentificadoresDeTeste.Novo());

        processo.DefinirCronogramaFases(
            [FaseDeResultadoFinal()],
            []).IsSuccess.Should().BeTrue();
        processo.DefinirEtapas([
            EtapaProcesso.Criar(
                new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: "RESULTADO_FINAL"),
                new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!,
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirAtendimentoEspecializado(
            AtendimentoEspecializado.Criar([], [], []).Value!).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            Guid.CreateVersion7(), "AC", null, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas,
            null, RegraRemanejamentoModalidade.Nenhuma, null, null, null, RegrasDeAplicabilidadeDeTeste.DaNatureza(NaturezaLegalModalidade.Ampla), null, "Res. Unifesspa 532/2021",
            quantidadeDeclarada: 40).Value!;
        processo.DefinirDistribuicaoVagas(
            [ConfiguracaoDistribuicaoVagas.Criar(Guid.CreateVersion7(),
                new DistribuicaoDeVagasDeclarada { VagasOfertadasBase = 40, PercentualDaReservaLegal = 1m, RegraDeDistribuicao = Regra(RegraDistribuicaoVagasCodigo.Institucional, 'a'), Modalidades = [modalidade] },
                new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!], FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = Regra(RegraCalculoCodigo.ClassificacaoImportada, 'b'),
            RegraOrdemAlocacao = Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, 'c'),
        }).Value!).IsSuccess.Should().BeTrue();

        // Issue #1112: publicar sem declarar cobrança de taxa é recusado (CA-01).
        processo.DefinirTaxaInscricao(
            ConfiguracaoTaxaInscricao.Criar(cobra: false, valor: null, fundamentosCodigos: null).Value!).IsSuccess.Should().BeTrue();
        DeclaracoesObrigatoriasDeTeste.Declarar(processo);

        processo.DefinirItens([
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ]).IsSuccess.Should().BeTrue();

        processo.DefinirTitulo("Formulário de Inscrição").IsSuccess.Should().BeTrue();
        processo.DefinirTermos([CorpusEnvelopeEntrada.Termo("DECLARACAO_VERACIDADE", 0)])
            .IsSuccess.Should().BeTrue();

        return processo;
    }

    /// <summary>
    /// Publica o processo e grava o processo, a versão publicada e a divulgação. No caminho real a
    /// divulgação nasce quando o ato normativo se confirma no registro central; aqui é semeada
    /// direto, porque o que se exercita é a projeção.
    /// </summary>
    public async Task<VersaoConfiguracao> PublicarEPersistirAsync(
        ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, string hashDocumento, string ator)
    {
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hashDocumento), AtorUsuarioContaId = ator, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() });
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, dados);
        return publicar.Value!;
    }

    public async Task<FormularioRenderizavelDto> LerPublicadoAsync(
        Guid processoId, FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> lido = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, finalidade), ProcessoSeletivoRepositoryDeTeste.Criar(readContext),
            new CertameDivulgadoRepository(readContext), RegistroCodecs,
            new EnderecosDosDocumentosDoAto(new CertameDivulgadoRepository(readContext), AcervoDeTeste.Endereco), CancellationToken.None);
        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        return lido.Value!;
    }

    public async Task<FormularioRenderizavelDto> LerRascunhoAsync(Guid processoId, IReadOnlyList<FatoCandidatoView> catalogoVivo)
    {
        IFatoCandidatoReader leitor = Substitute.For<IFatoCandidatoReader>();
        leitor.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogoVivo);
        await using UniPlusDbContext rascunhoContext = _fixture.CreateDbContext();
        Result<FormularioRenderizavelDto> rascunho = await ObterFormularioRenderizavelDoRascunhoQueryHandler.Handle(
            new ObterFormularioRenderizavelDoRascunhoQuery(processoId, FinalidadeFormulario.Inscricao),
            ProcessoSeletivoRepositoryDeTeste.Criar(rascunhoContext), leitor, FusoInstitucionalDeTeste(), CancellationToken.None);

        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        return rascunho.Value!;
    }

    private static IResolvedorFusoInstitucional FusoInstitucionalDeTeste()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    private async Task PersistirDivulgadoAsync(ProcessoSeletivo processo, VersaoConfiguracao versao, DadosEdital dados)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await new CertameDivulgadoRepository(writeContext).AdicionarAsync(
            CertameDivulgado.Criar(
                processo.Id,
                versao.NumeroVersao,
                versao.AtoCriadorId,
                new string('a', 64),
                versaoProjecao: "1",
                new FacetasDoCertameDivulgado(
                    processo.IdentificadorLegivel!.Value.Valor, processo.Nome, dados.Numero, ["AC"], dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim),
                """{"nome":"documento"}""",
                TimeProvider.System.GetUtcNow()),
            CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }
}
