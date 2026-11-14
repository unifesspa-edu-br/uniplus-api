namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// Um processo publicado e as dependências dos dois caminhos que congelam uma retificação: o atalho sem sessão
/// e o fechamento da sessão. O registro de codecs reidrata a versão publicada real, porque um registro sem
/// versão faria os testes passarem com a conferência retirada.
/// </summary>
internal sealed class CenarioDeRetificacaoPublicada
{
    private readonly DependenciasDosHandlersQueCongelam _handlers;
    private readonly Dictionary<string, TipoDeAtoCongelado> _tiposCongelados = new(StringComparer.Ordinal);
    private readonly VersaoConfiguracao _versao;
    private readonly GrafoConfiguracao _grafoPublicado;
    private readonly EixosDeVersaoPublicada _congelado;

    private CenarioDeRetificacaoPublicada(ProcessoSeletivo processo, VersaoConfiguracao versao, GrafoConfiguracao grafoPublicado, EixosDeVersaoPublicada congelado)
    {
        _handlers = DependenciasDosHandlersQueCongelam.Para(processo);
        _versao = versao;
        _grafoPublicado = grafoPublicado;
        _congelado = congelado;
        _handlers.Codecs = Substitute.For<IRegistroCodecsEnvelope>();
        _handlers.CatalogoDeFatos = Substitute.For<IFatoCandidatoReader>();
        _handlers.Canonicalizer = CanonicalizerSubstituto(entrada => EntradaCanonicalizada = entrada);
    }

    public ProcessoSeletivo Processo => _handlers.Processo;

    public ServicosDoCongelamentoDeTeste Servicos => _handlers.Servicos;

    /// <summary>A entrada que o handler entregou ao canonicalizador na última chamada.</summary>
    public EntradaCanonicalizacao? EntradaCanonicalizada { get; private set; }

    public static CenarioDeRetificacaoPublicada Publicado(SituacaoDoFatoCitado? situacao = null)
    {
        SituacaoDoFatoCitado fato = situacao ?? new SituacaoDoFatoCitado();
        ProcessoSeletivo processo = ProcessoQueColetaOFato(fato.ComItemNovo);
        VersaoConfiguracao versao = Publicar(processo);
        CenarioDeRetificacaoPublicada cenario = new(processo, versao, GrafoPublicado(processo, fato), EixosCongelados(fato.DominioCongelado));
        cenario.Configurar(fato);

        return cenario;
    }

    public void AbrirSessao() =>
        Processo.AbrirRetificacao(
            "Correção do prazo", _versao, identificadorDaVersaoBase: null,
            FatosDasVersoesPublicadas.De([_grafoPublicado], [_congelado]), "user-sub-1", Agora)
            .IsSuccess.Should().BeTrue();

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync() => _handlers.RetificarAsync();

    public Task<(Result Resposta, IEnumerable<object> Eventos)> FecharAsync() => _handlers.FecharRetificacaoAsync();

    public string ClassificacaoDoItem(string fato) => Processo.FatosColetados.Single(f => f.FatoCodigo == fato).ClassificacaoProtecao;

    /// <summary>O cadastro vivo das unidades: só as informadas estão vivas.</summary>
    public void ComUnidadesVivas(params UnidadeResponsavelSnapshot[] vivas) =>
        _handlers.Unidades = ResolvedorDeUnidadeDeTeste.Com(vivas);

    /// <summary>O processo passa a cobrar a taxa com um fundamento de isenção e não escolheu motivo algum.</summary>
    public void ComIsencaoHabilitadaSemMotivos() => IsencaoDeTeste.HabilitarSemMotivos(Processo);

    /// <summary>O tipo da exigência voltou a pendente na proteção de dados depois de vinculado.</summary>
    public void ComTipoDaExigenciaPendente()
    {
        _handlers.TiposDocumento = Substitute.For<ITipoDocumentoReader>();
        _handlers.TiposDocumento.ListarVivosAsync(Arg.Any<CancellationToken>())
            .Returns([CadastrosVivos.TipoDocumento("LAUDO_MEDICO") with { ProtecaoAprovada = false }]);
    }

    /// <summary>O fato que o gatilho cita voltou a pendente na proteção de dados depois de vinculado.</summary>
    public async Task ComFatoCitadoPendenteAsync()
    {
        IReadOnlyList<FatoCandidatoView> catalogo = await _handlers.CatalogoDeFatos.ListarAsync(CancellationToken.None);
        _handlers.CatalogoDeFatos.ListarAsync(Arg.Any<CancellationToken>())
            .Returns([.. catalogo.Select(f => f.Codigo == Fato ? f with { ProtecaoAprovada = false } : f)]);
    }

    /// <summary>
    /// A versão publicada congelou o resultado do cronograma como tipo comum (não único, não congela), e
    /// o catálogo de hoje o declara único e congelante.
    /// </summary>
    public void ComResultadoCongeladoComoComumEUnicoNoCatalogo()
    {
        _tiposCongelados[FasesDoCronogramaDeTeste.AtoDaClassificacao] = new TipoDeAtoCongelado(FasesDoCronogramaDeTeste.AtoDaClassificacao, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);
        _handlers.TiposDeAto.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new TipoAtoPublicadoView(FasesDoCronogramaDeTeste.AtoDaClassificacao, "Resultado", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true)]);
    }

    /// <summary>
    /// A versão publicada declara o par preliminar e definitivo da mesma matéria e o preliminar já saiu;
    /// o processo em edição, que não declara cronograma, ficou sem os dois.
    /// </summary>
    public void ComPreliminarPublicadoEDefinitivoPendente()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar);
        FaseCronograma fase = FaseDaProvaComPreliminarEDefinitivo(preliminar);
        _handlers.Codecs.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(EnvelopeDaVersaoComAFase(fase)));
        _handlers.Atos.ListarDoProcessoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns([PreliminarJaPublicado(Processo.Id, fase, preliminar)]);
    }

    private static GrafoConfiguracao GrafoPublicado(ProcessoSeletivo processo, SituacaoDoFatoCitado fato) =>
        fato.PublicadoDivergeDoProcesso
            ? VersoesPublicadasDeTeste.DoProcesso(ProcessoSoComOItemPublicado(fato.TipoDoItemPublicado))
            : VersoesPublicadasDeTeste.DoProcesso(processo);

    private static FaseCronograma FaseDaProvaComPreliminarEDefinitivo(ProdutoDaFase preliminar) =>
        FaseCronograma.Criar(
            1, Guid.CreateVersion7(), "PROVA", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: Agora, fim: Agora.AddDays(10),
            produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo)],
            faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static AtoDoProcesso PreliminarJaPublicado(Guid processoId, FaseCronograma fase, ProdutoDaFase preliminar)
    {
        RelogioFixo relogio = new(Agora);
        AtoDoProcesso publicado = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, "RESULTADO_PROVA", "Resultado da prova", "1/2026", new DateOnly(2026, 1, 20),
                "Diretor do CEPS", "ceps", "ps-eixos", Guid.CreateVersion7(), [], ItemDoEdital: IdentidadeDoItemDoEdital.DaFase(fase, preliminar)),
            relogio.GetUtcNow()).Value!;
        publicado.ConfirmarRegistro(relogio);

        return publicado;
    }

    private EnvelopeReidratado EnvelopeDaVersaoComAFase(FaseCronograma fase)
    {
        GrafoConfiguracao grafo = new(
            [.. Processo.Etapas], Processo.OfertaAtendimento!, [.. Processo.DistribuicaoVagas], Processo.BonusRegional,
            [.. Processo.CriteriosDesempate], Processo.Classificacao!, [fase], [.. Processo.DocumentosExigidos], [], null,
            numeroDeOpcoesDeCurso: 1);

        return new EnvelopeReidratado(
            grafo, DadosEdital.Criar("001/2026", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            ProcessoSeletivoConformeBuilder.HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null);
    }

    private void Configurar(SituacaoDoFatoCitado fato)
    {
        _handlers.Repositorio.ObterVersaoAtualAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(_versao);
        _handlers.Repositorio.ObterVersoesAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns([_versao]);
        ConfigurarCodecs(fato.Reidratavel);
        ConfigurarCatalogoDeFatos(fato);
    }

    private void ConfigurarCodecs(bool reidratavel)
    {
        EnvelopeReidratado envelope = new(
            _grafoPublicado, DadosDoEdital(), ProcessoSeletivoConformeBuilder.HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null,
            metadadosFatosCongelados: new Dictionary<string, MetadadoFatoCongelado> { [Fato] = MetadadoCongelado(_congelado) },
            tiposDeAtoCongelados: _tiposCongelados);
        _handlers.Codecs.Capacidades.Returns(reidratavel ? [new CapacidadeCodec(_versao.SchemaVersion, true, true, null)] : []);
        _handlers.Codecs.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(envelope));
    }

    // O catálogo vivo diz o que a versão nova congelaria para o fato citado.
    private void ConfigurarCatalogoDeFatos(SituacaoDoFatoCitado fato)
    {
        FatoCandidatoView vivo = FatoDoCatalogo(fato.DominioNoCatalogo, fato.ClassificacaoNoCatalogo);
        _handlers.CatalogoDeFatos.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[vivo, FatoNovoDoCatalogo(vivo)]));
    }
}
