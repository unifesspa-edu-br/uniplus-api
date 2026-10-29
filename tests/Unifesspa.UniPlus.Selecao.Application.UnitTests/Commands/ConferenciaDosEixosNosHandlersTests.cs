namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os dois caminhos que congelam uma retificação conferem o que a versão nova congela contra o que as
/// versões publicadas congelaram (UNI-REQ-0144): o atalho sem sessão e o fechamento da sessão. O
/// registro de codecs reidrata versões reais, porque um registro sem versão faria os testes passarem
/// com a conferência retirada.
/// </summary>
public sealed class ConferenciaDosEixosNosHandlersTests
{
    private const string Fato = "TEM_RENDA";
    private const string FatoNovo = "TEM_ENDERECO_PROPRIO";
    private const string Resultado = "RESULTADO_CLASSIFICACAO";
    private const string Recusa = "RascunhoRetificacao.EixoDoFatoJaColetadoAlterado";
    private static readonly string HashFixo = ProcessoSeletivoConformeBuilder.HashFixo;
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Retificar recusa o fato citado por condição de documento cujo domínio o catálogo trocou desde a versão publicada")]
    public async Task Retificar_DominioDoFatoCitadoTrocado_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "CATEGORICO");

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be(Recusa);
        resposta.Error.Message.Should().Contain(Fato).And.Contain("domínio");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar sem versão reidratável recusa, como a abertura da sessão, em vez de pular a versão")]
    public async Task Retificar_VersaoNaoReidratavel_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", reidratavel: false);

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
        resposta.Error.Message.Should().NotContain("descartada", "sem sessão não há retificação a descartar");
    }

    [Fact(DisplayName = "Retificação só de datas passa com o catálogo editado num eixo de item coletado que o item não regravou")]
    public async Task Retificar_CatalogoEditadoNoEixoDoItemSemRegravar_Passa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", classificacaoNoCatalogo: "SENSIVEL");

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Fechar a retificação recusa o fato citado por condição de documento cujo domínio o catálogo trocou desde a versão publicada")]
    public async Task Fechar_DominioDoFatoCitadoTrocado_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "CATEGORICO");
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(Recusa);
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação recusa o item cujo eixo chegou à configuração diferente do que a versão publicada congelou")]
    public async Task Fechar_ItemComEixoTrocado_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", itemPublicadoComoData: true);
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(Recusa);
        resposta.Error.Message.Should().Contain(Fato).And.Contain("tipo de renderização");
    }

    [Fact(DisplayName = "Fechar a retificação sem troca de eixo passa mesmo com o catálogo editado num eixo de item coletado")]
    public async Task Fechar_CatalogoEditadoNoEixoDoItemSemRegravar_Passa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", classificacaoNoCatalogo: "SENSIVEL");
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Retificar recopia do catálogo o fato novo e deixa o já coletado com a cópia que a versão publicada congelou")]
    public async Task Retificar_FatosReclassificadosNoCatalogo_RecopiaSoOFatoNovo()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", classificacaoNoCatalogo: "IDENTIFICADOR", comItemNovo: true);

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.ClassificacaoDoItem(FatoNovo).Should().Be("IDENTIFICADOR");
        cenario.ClassificacaoDoItem(Fato).Should().Be("PESSOAL", "a versão publicada já coletou o fato com essa cópia");
    }

    [Fact(DisplayName = "Retificar recusa o fato já coletado cujo campo ficou incoerente com o catálogo, orientando desfazer a correção")]
    public async Task Retificar_FatoJaColetadoIncoerenteComOCatalogo_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", dominioNoCatalogo: "CATEGORICO");

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().MatchRegex(@"^formularios\[[A-Z_]+\]\.itens\[\d+\]\.tipoRenderizacao$");
        resposta.Error!.Message.Should().Contain("desfaça a correção no catálogo");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fechar a retificação recopia do catálogo o fato novo e deixa o já coletado com a cópia que a versão publicada congelou")]
    public async Task Fechar_FatosReclassificadosNoCatalogo_RecopiaSoOFatoNovo()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", classificacaoNoCatalogo: "IDENTIFICADOR", comItemNovo: true);
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.ClassificacaoDoItem(FatoNovo).Should().Be("IDENTIFICADOR");
        cenario.ClassificacaoDoItem(Fato).Should().Be("PESSOAL", "a versão publicada já coletou o fato com essa cópia");
    }

    [Fact(DisplayName = "Fechar a retificação recusa o fato já coletado cujo campo ficou incoerente com o catálogo, orientando desfazer a correção")]
    public async Task Fechar_FatoJaColetadoIncoerenteComOCatalogo_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO", dominioNoCatalogo: "CATEGORICO");
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().MatchRegex(@"^formularios\[[A-Z_]+\]\.itens\[\d+\]\.tipoRenderizacao$");
        resposta.Error!.Message.Should().Contain("desfaça a correção no catálogo");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa a configuração que retira o definitivo de uma matéria cujo preliminar já foi publicado")]
    public async Task Retificar_RetiraODefinitivoDeMateriaComPreliminarPublicado_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.ComPreliminarPublicadoEDefinitivoPendente();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Fechar a retificação recusa a configuração que retira o definitivo de uma matéria cujo preliminar já foi publicado")]
    public async Task Fechar_RetiraODefinitivoDeMateriaComPreliminarPublicado_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.AbrirSessao();
        cenario.ComPreliminarPublicadoEDefinitivoPendente();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.RetificacaoDesfazCicloRecursalAberto);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar mantém a classificação que a versão vigente congelou para o tipo: único e congelante no catálogo de hoje não o recusa nem muda o que a versão nova congela")]
    public async Task Retificar_TipoJaCongelado_MantemAClassificacaoDaVersaoVigente()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.ComResultadoCongeladoComoComumEUnicoNoCatalogo();

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.TiposDeAtoCongelados![Resultado].Should().Be(
            new TipoDeAtoCongelado(Resultado, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
    }

    [Fact(DisplayName = "Fechar a retificação mantém a classificação que a versão vigente congelou para o tipo: único e congelante no catálogo de hoje não o recusa nem muda o que a versão nova congela")]
    public async Task FecharRetificacao_TipoJaCongelado_MantemAClassificacaoDaVersaoVigente()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.ComResultadoCongeladoComoComumEUnicoNoCatalogo();
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.TiposDeAtoCongelados![Resultado].Should().Be(
            new TipoDeAtoCongelado(Resultado, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
    }

    private static readonly UnidadeResponsavelSnapshot CepsRenomeada = new(UnidadesDeTeste.Ceps.OrigemId, "CEPS", "Centro de Processos Seletivos e Concursos");

    [Fact(DisplayName = "Retificar recusa a unidade responsável que o cadastro excluiu depois do cronograma")]
    public async Task Retificar_UnidadeExcluida_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.Unidades = ResolvedorDeUnidadeDeTeste.Com();

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("fases[0].unidadeResponsavelId");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar congela a unidade responsável com a sigla e o nome de agora")]
    public async Task Retificar_UnidadeRenomeada_CongelaONomeNovo()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.Unidades = ResolvedorDeUnidadeDeTeste.Com(CepsRenomeada);

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.Processo.CronogramaFases.Should().OnlyContain(f => f.UnidadeResponsavelNome == CepsRenomeada.Nome);
    }

    [Fact(DisplayName = "Fechar a retificação recusa a unidade responsável que o cadastro excluiu depois do cronograma")]
    public async Task Fechar_UnidadeExcluida_Recusa()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.AbrirSessao();
        cenario.Unidades = ResolvedorDeUnidadeDeTeste.Com();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação congela a unidade responsável com a sigla e o nome de agora")]
    public async Task Fechar_UnidadeRenomeada_CongelaONomeNovo()
    {
        Cenario cenario = Cenario.Publicado(dominioCongelado: "BOOLEANO");
        cenario.AbrirSessao();
        cenario.Unidades = ResolvedorDeUnidadeDeTeste.Com(CepsRenomeada);

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.EntradaCanonicalizada!.Processo.CronogramaFases.Should().OnlyContain(f => f.UnidadeResponsavelNome == CepsRenomeada.Nome);
    }

    private sealed class Cenario
    {
        private readonly IProcessoSeletivoRepository _repositorio = Substitute.For<IProcessoSeletivoRepository>();
        private readonly IRegistroCodecsEnvelope _registro = Substitute.For<IRegistroCodecsEnvelope>();
        private readonly IFatoCandidatoReader _catalogo = Substitute.For<IFatoCandidatoReader>();
        private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
        private readonly Dictionary<string, TipoDeAtoCongelado> _tiposCongelados = new(StringComparer.Ordinal);
        private readonly ITipoAtoPublicadoReader _tiposDeAto = TipoDeAtoReader();
        private readonly VersaoConfiguracao _versao;
        private readonly GrafoConfiguracao _grafoPublicado;
        private readonly EixosDeVersaoPublicada _congelado;

        private Cenario(ProcessoSeletivo processo, VersaoConfiguracao versao, GrafoConfiguracao grafoPublicado, EixosDeVersaoPublicada congelado)
        {
            Processo = processo;
            _versao = versao;
            _grafoPublicado = grafoPublicado;
            _congelado = congelado;
        }

        public ProcessoSeletivo Processo { get; }

        /// <summary>O cadastro vivo das unidades; por padrão, as de teste estão todas vivas.</summary>
        public IResolvedorDaUnidadeResponsavel Unidades { get; set; } = ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste();

        /// <summary>A entrada que o handler entregou ao canonicalizador na última chamada.</summary>
        public EntradaCanonicalizacao? EntradaCanonicalizada { get; private set; }

        /// <summary>
        /// A versão publicada congelou o resultado do cronograma como tipo comum (não único, não congela), e
        /// o catálogo de hoje o declara único e congelante.
        /// </summary>
        public void ComResultadoCongeladoComoComumEUnicoNoCatalogo()
        {
            _tiposCongelados[Resultado] = new TipoDeAtoCongelado(Resultado, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);
            _tiposDeAto.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns([new TipoAtoPublicadoView(Resultado, "Resultado", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true)]);
        }

        public static Cenario Publicado(
            string dominioCongelado, bool reidratavel = true, string classificacaoNoCatalogo = "PESSOAL", bool itemPublicadoComoData = false,
            bool comItemNovo = false, string dominioNoCatalogo = "BOOLEANO")
        {
            ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS Eixos nos handlers", out Guid faseId);
            processo.DefinirItens(
                comItemNovo ? [Item(TipoRenderizacao.Booleano), ItemNovo()] : [Item(TipoRenderizacao.Booleano)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(ExigenciaComGatilho(faseId), 0).Value!], PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();

            DadosEdital dados = DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!;
            Result<VersaoConfiguracao> versao = processo.Publicar(
                dados, EnvelopeCongeladoDeTeste.SemAnexos(), "1.1", "canonical-json/sha256@v1", HashFixo, "user-sub-1",
                new RelogioFixo(), ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum);
            versao.IsSuccess.Should().BeTrue(versao.Error?.Message);
            processo.DequeueDomainEvents();

            // A versão publicada coletou só o fato de TEM_RENDA; o fato novo veio depois.
            GrafoConfiguracao publicado = itemPublicadoComoData || comItemNovo
                ? VersoesPublicadasDeTeste.DoProcesso(OutroComOItem(itemPublicadoComoData ? TipoRenderizacao.Data : TipoRenderizacao.Booleano))
                : VersoesPublicadasDeTeste.DoProcesso(processo);
            EixosDeVersaoPublicada congelado = new(
                new Dictionary<string, EixosDoMetadadoCongelado> { [Fato] = new(dominioCongelado, "ESCALAR", "RESULTADO_FINAL") },
                new Dictionary<string, string>());

            Cenario cenario = new(processo, versao.Value!, publicado, congelado);
            cenario.Configurar(reidratavel, classificacaoNoCatalogo, dominioNoCatalogo);
            return cenario;
        }

        public void AbrirSessao() =>
            Processo.AbrirRetificacao(
                "Correção do prazo", _versao, identificadorDaVersaoBase: null,
                FatosDasVersoesPublicadas.De([_grafoPublicado], [_congelado]), "user-sub-1", Agora)
                .IsSuccess.Should().BeTrue();

        public Task<(Result, IEnumerable<object>)> RetificarAsync() => RetificarProcessoSeletivoCommandHandler.Handle(
            new RetificarProcessoSeletivoCommand(
                Processo.Id, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto()),
            _repositorio,
            RepositorioDeDocumento(Processo.Id),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            _atos,
            Canonicalizer(e => EntradaCanonicalizada = e), new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            _tiposDeAto,
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            _catalogo,
            CalendarioVigenteReaderDeTeste.SemVigente(),
            Unidades,
            _registro,
            new RelogioFixo(),
            CancellationToken.None);

        public Task<(Result, IEnumerable<object>)> FecharAsync() => FecharRetificacaoCommandHandler.Handle(
            new FecharRetificacaoCommand(
                Processo.Id, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga),
            _repositorio,
            RepositorioDeDocumento(Processo.Id),
            Substitute.For<IRascunhoDePublicacaoRepository>(),
            _atos,
            Canonicalizer(e => EntradaCanonicalizada = e), new ResolvedorFusoDeTeste(),
            Substitute.For<ISelecaoUnitOfWork>(),
            UsuarioAutenticado(),
            _tiposDeAto,
            Substitute.For<IVagaDeLinhagemReader>(),
            Substitute.For<IObrigatoriedadeLegalRepository>(),
            CadastrosVivos.Modalidades(),
            CadastrosVivos.TiposDocumento(),
            CadastrosVivos.TiposEtapa(),
            CadastrosVivos.TiposDeficiencia(),
            CadastrosVivos.RegrasDesempate(),
            _catalogo,
            CalendarioVigenteReaderDeTeste.SemVigente(),
            Unidades,
            _registro,
            new RelogioFixo(),
            CancellationToken.None);

        /// <summary>
        /// A versão publicada declara o par preliminar e definitivo da mesma matéria e o preliminar já saiu;
        /// o processo em edição, que não declara cronograma, ficou sem os dois.
        /// </summary>
        public void ComPreliminarPublicadoEDefinitivoPendente()
        {
            ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar);
            FaseCronograma fase = FaseCronograma.Criar(
                1, Guid.CreateVersion7(), "PROVA", OrigemDataFase.NoEdital,
                agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
                inicio: Agora, fim: Agora.AddDays(10),
                produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo)],
                faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
            GrafoConfiguracao grafo = new(
                [.. Processo.Etapas], Processo.OfertaAtendimento!, [.. Processo.DistribuicaoVagas], Processo.BonusRegional,
                [.. Processo.CriteriosDesempate], Processo.Classificacao!, [fase], [.. Processo.DocumentosExigidos], [], null,
                numeroDeOpcoesDeCurso: 1);
            _registro.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
                grafo, DadosEdital.Criar("001/2026", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
                HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null)));

            AtoDoProcesso publicado = AtoDoProcesso.Registrar(
                new NovoAtoDoProcesso(
                    Guid.CreateVersion7(), Processo.Id, "RESULTADO_PROVA", "Resultado da prova", "1/2026", new DateOnly(2026, 1, 20),
                    "Diretor do CEPS", "ceps", "ps-eixos", Guid.CreateVersion7(), [], ItemDoEdital: IdentidadeDoItemDoEdital.DaFase(fase, preliminar)),
                new RelogioFixo()).Value!;
            publicado.ConfirmarRegistro(new RelogioFixo());
            _atos.ListarDoProcessoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns([publicado]);
        }

        private void Configurar(bool reidratavel, string classificacaoNoCatalogo, string dominioNoCatalogo)
        {
            _repositorio.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(Processo);
            _repositorio.ObterVersaoAtualAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(_versao);
            _repositorio.ObterVersoesAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns([_versao]);

            // O catálogo vivo diz BOOLEANO: é o que a versão nova congelaria para o fato citado.
            MetadadoFatoCongelado publicado = new(Fato, _congelado.Metadados[Fato].Dominio, "DECLARADO", "ESCALAR", "RESULTADO_FINAL", $"CAMPO_FORMULARIO:{Fato}", null, null);
            _registro.Capacidades.Returns(reidratavel ? [new CapacidadeCodec(_versao.SchemaVersion, true, true, null)] : []);
            _registro.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
                _grafoPublicado, DadosEdital.Criar(
                    "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                    new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), Guid.CreateVersion7()).Value!,
                HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null,
                metadadosFatosCongelados: new Dictionary<string, MetadadoFatoCongelado> { [Fato] = publicado },
                tiposDeAtoCongelados: _tiposCongelados)));

            FatoCandidatoView vivo = new(
                Guid.CreateVersion7(), Fato, "Tem renda?", null, dominioNoCatalogo, "DECLARADO", "ESCALAR", null,
                "RESULTADO_FINAL", $"CAMPO_FORMULARIO:{Fato}", null, null, Ativo: true, ClassificacaoProtecao: classificacaoNoCatalogo);
            FatoCandidatoView novo = vivo with { Id = Guid.CreateVersion7(), Codigo = FatoNovo, Nome = "Tem endereço próprio?", Dominio = "BOOLEANO", Binding = $"CAMPO_FORMULARIO:{FatoNovo}" };
            _catalogo.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[vivo, novo]));
        }

        public string ClassificacaoDoItem(string fato) => Processo.FatosColetados.Single(f => f.FatoCodigo == fato).ClassificacaoProtecao;

        // O que a versão publicada congelou do item vem de um processo que só coleta o fato com esse tipo.
        private static ProcessoSeletivo OutroComOItem(TipoRenderizacao tipo)
        {
            ProcessoSeletivo outro = ProcessoSeletivoConformeBuilder.Criar("PS Item publicado");
            outro.DefinirItens([Item(tipo)], PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            return outro;
        }

        // Um fato que nenhuma versão publicada coletou, com a cópia velha do catálogo (PESSOAL).
        private static FatoColetado ItemNovo() =>
            FatoColetado.Criar(FatoNovo, 100, "Tem endereço próprio?", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL").Value!;

        private static FatoColetado Item(TipoRenderizacao tipo) =>
            FatoColetado.Criar(Fato, 0, "Tem renda?", tipo, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL").Value!;

        private static DocumentoExigido ExigenciaComGatilho(Guid faseId) =>
            DocumentoExigido.Criar(
                faseId, Guid.CreateVersion7(), "DECLARACAO", "Declaração", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
                Aplicabilidade.Condicional, obrigatorio: true, consequenciaIndeferimento: null,
                condicoes: [CondicaoGatilho.Criar(0, Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
                basesLegais: [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
                idadeMaximaEmissao: null, formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
                tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao).Value!;

        private static ISnapshotPublicacaoCanonicalizer Canonicalizer(Action<EntradaCanonicalizacao> captura)
        {
            ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
            canonicalizer.Canonicalizar(Arg.Any<EntradaCanonicalizacao>())
                .Returns(chamada =>
                {
                    captura(chamada.Arg<EntradaCanonicalizacao>());
                    return new SnapshotCanonico("{}"u8.ToArray(), "1.1", "canonical-json/sha256@v1");
                });
            return canonicalizer;
        }

        private static IDocumentoAPublicarRepository RepositorioDeDocumento(Guid processoSeletivoId)
        {
            DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoSeletivoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
            IDocumentoAPublicarRepository repositorio = Substitute.For<IDocumentoAPublicarRepository>();
            repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(documento);
            return repositorio;
        }

        private static ITipoAtoPublicadoReader TipoDeAtoReader()
        {
            ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>();
            reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns(new TipoAtoPublicadoView(
                    Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura", CongelaConfiguracao: true,
                    UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
            return reader.ComQualquerTipoDeclarado();
        }

        private static IUserContext UsuarioAutenticado()
        {
            IUserContext contexto = Substitute.For<IUserContext>();
            contexto.UserId.Returns("user-sub-1");
            return contexto;
        }

        private static DadosDoAto NovoAto() => new(
            Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataPublicacao: new DateOnly(2026, 1, 1),
            Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA");
    }

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
