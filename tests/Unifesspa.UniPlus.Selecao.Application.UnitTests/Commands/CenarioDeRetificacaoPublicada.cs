namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
/// Um processo publicado e as dependências do caminho que congela uma retificação. O registro de codecs reidrata a
/// versão publicada real, porque um registro sem versão faria os testes passarem com a conferência retirada.
/// </summary>
internal sealed class CenarioDeRetificacaoPublicada
{
    private readonly DependenciasDosHandlersQueCongelam _handlers;
    private readonly Dictionary<string, TipoDeAtoCongelado> _tiposCongelados = new(StringComparer.Ordinal);
    private readonly VersaoConfiguracao _versao;
    private ConfiguracaoPublicada _configuracaoPublicada;
    private readonly EixosDeVersaoPublicada _congelado;

    private CenarioDeRetificacaoPublicada(ProcessoSeletivo processo, VersaoConfiguracao versao, ConfiguracaoPublicada configuracaoPublicada, EixosDeVersaoPublicada congelado)
    {
        _handlers = DependenciasDosHandlersQueCongelam.Para(processo);
        _versao = versao;
        _configuracaoPublicada = configuracaoPublicada;
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
        ProcessoSeletivo processo = ProcessoQueColetaOFato(fato.ComItemNovo, fato.ExigenciaCitaFatoAusenteDoCatalogo);
        VersaoConfiguracao versao = Publicar(processo);
        CenarioDeRetificacaoPublicada cenario = new(processo, versao, ConfiguracaoPublicadaDe(processo, fato), EixosCongelados(fato.DominioCongelado));
        cenario.Configurar(fato);

        return cenario;
    }

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync() => _handlers.RetificarAsync();

    public string ClassificacaoDoItem(string fato) => Processo.FatosColetados.Single(f => f.FatoCodigo == fato).ClassificacaoProtecao;

    /// <summary>O cadastro vivo das unidades: só as informadas estão vivas.</summary>
    public void ComUnidadesVivas(params UnidadeResponsavelSnapshot[] vivas) =>
        _handlers.Unidades = ResolvedorDeUnidadeDeTeste.Com(vivas);

    /// <summary>A versão publicada declarou o mesmo cronograma que o processo tem agora.</summary>
    public void ComCronogramaPublicadoIgualAoDoProcesso() => ComCronogramaPublicado([.. Processo.CronogramaFases]);

    /// <summary>A versão publicada declarou um cronograma sem fases, que difere do que o processo tem agora.</summary>
    public void ComCronogramaPublicadoDiferenteDoDoProcesso() => ComCronogramaPublicado([]);

    private void ComCronogramaPublicado(IReadOnlyList<FaseCronograma> fases)
    {
        _configuracaoPublicada = ConfiguracaoPublicadaDeTeste.ComCronograma(
            _configuracaoPublicada, new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, fases));
        ConfigurarCodecs(reidratavel: true);
    }

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
        _tiposCongelados[FasesDoCronogramaDeTeste.AtoDaClassificacao] = new TipoDeAtoCongelado(FasesDoCronogramaDeTeste.AtoDaClassificacao, Nome: "Nome do ato", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);
        _handlers.TiposDeAto.ListarVigentesAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([new TipoAtoPublicadoView(FasesDoCronogramaDeTeste.AtoDaClassificacao, "Resultado", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: true)]);
    }

    /// <summary>
    /// A versão publicada declara o par preliminar e definitivo da mesma matéria e o preliminar já saiu;
    /// o processo em edição, que não declara cronograma, ficou sem os dois.
    /// </summary>
    public void ComPreliminarPublicadoEDefinitivoPendente()
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_PROVA", PapelDaPublicacaoPrevista.Preliminar);
        FaseCronograma fase = FaseDaProvaComPreliminarEDefinitivo(preliminar);
        _handlers.Codecs.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(EnvelopeDaVersaoComAFase(fase)));
        _handlers.Atos.ListarDoProcessoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns([PreliminarJaPublicado(Processo.Id, fase, preliminar)]);
    }

    private static ConfiguracaoPublicada ConfiguracaoPublicadaDe(ProcessoSeletivo processo, SituacaoDoFatoCitado fato) =>
        fato.PublicadoDivergeDoProcesso
            ? VersoesPublicadasDeTeste.DoProcesso(ProcessoSoComOItemPublicado(fato.TipoDoItemPublicado))
            : VersoesPublicadasDeTeste.DoProcesso(processo);

    private static FaseCronograma FaseDaProvaComPreliminarEDefinitivo(ProdutoDaFase preliminar) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), "PROVA", OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(1, Inicio: Agora, Fim: Agora.AddDays(10), UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [preliminar, ProdutoDaFase.Criar("RESULTADO_PROVA", PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    private static AtoDoProcesso PreliminarJaPublicado(Guid processoId, FaseCronograma fase, ProdutoDaFase preliminar)
    {
        RelogioFixo relogio = new(Agora);
        return AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, "RESULTADO_PROVA", "Resultado da prova", "1/2026", new DateOnly(2026, 1, 20),
                "Diretor do CEPS", "ceps", "ps-eixos", Guid.CreateVersion7(), [], ItemDoEdital: IdentidadeDoItemDoEdital.DaFase(fase, preliminar)),
            relogio.GetUtcNow()).Value!;
    }

    private EnvelopeReidratado EnvelopeDaVersaoComAFase(FaseCronograma fase)
    {
        ConfiguracaoPublicada configuracao = new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [fase]),
            new AvaliacaoDeclarada([.. Processo.Etapas], [.. Processo.CriteriosDesempate], Processo.Classificacao!),
            new VagasDeclaradas([.. Processo.DistribuicaoVagas], 1, Processo.Fada is not null,
                Processo.Fada, null, Processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. Processo.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

        return new EnvelopeReidratado(
            configuracao, DadosEdital.Criar("001/2026", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            ProcessoSeletivoConformeBuilder.HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada());
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
            _configuracaoPublicada, DadosDoEdital(), ProcessoSeletivoConformeBuilder.HashFixo, "America/Sao_Paulo", retificacao: null, conformidade: null,
            dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(
                MetadadosFatosCongelados: new Dictionary<string, MetadadoFatoCongelado> { [Fato] = MetadadoCongelado(_congelado) },
                TiposDeAtoCongelados: _tiposCongelados));
        _handlers.Codecs.LeAVersao(_versao.SchemaVersion).Returns(reidratavel);
        _handlers.Codecs.Reidratar(_versao).Returns(Result<EnvelopeReidratado>.Success(envelope));
    }

    // O catálogo vivo diz o que a versão nova congelaria para o fato citado.
    private void ConfigurarCatalogoDeFatos(SituacaoDoFatoCitado fato)
    {
        FatoCandidatoView vivo = FatoDoCatalogo(fato.DominioNoCatalogo, fato.ClassificacaoNoCatalogo);
        FatoCandidatoView citado = fato.FatoCitadoReclassificadoNoCatalogo ? vivo with { Origem = CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado) } : vivo;
        FatoCandidatoView novo = FatoNovoDoCatalogo(vivo);
        _handlers.CatalogoDeFatos.ListarAsync(Arg.Any<CancellationToken>())
            .Returns(CatalogoDoConjuntoBasico.Com((IReadOnlyList<FatoCandidatoView>)[citado, fato.ItemNovoReclassificadoNoCatalogo ? novo with { Origem = CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado) } : novo]));
    }
}
