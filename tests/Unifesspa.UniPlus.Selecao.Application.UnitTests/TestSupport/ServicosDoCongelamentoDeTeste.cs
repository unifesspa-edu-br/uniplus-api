namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.DublesDoCongelamentoDeTeste;

/// <summary>
/// As portas de que os serviços do congelamento dependem, com o que a suíte usa, e a construção de cada serviço
/// sobre elas no instante da chamada. O teste troca com <c>Com...</c> só a porta que quer descrever; o resto fica
/// como o handler real o receberia do contêiner.
/// </summary>
internal sealed class ServicosDoCongelamentoDeTeste
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static IEnderecoNoAcervoPublico EnderecoNoAcervo()
    {
        IEnderecoNoAcervoPublico endereco = Substitute.For<IEnderecoNoAcervoPublico>();
        endereco.De(Arg.Any<string>()).Returns(static chamada => new Uri($"https://acervo.teste/{chamada.Arg<string>()}"));
        return endereco;
    }

    /// <summary>A porta de escrita do registro central que confere e aceita o ato, sem reconhecer conflito algum na gravação.</summary>
    public static IRegistroDeAtoNormativo RegistroQueAceita()
    {
        IRegistroDeAtoNormativo registro = Substitute.For<IRegistroDeAtoNormativo>();
        registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        registro.TraduzirConflitoDaGravacao(Arg.Any<Exception>()).Returns((DomainError?)null);

        return registro;
    }

    public IProcessoSeletivoRepository Repositorio { get; private set; } = Substitute.For<IProcessoSeletivoRepository>();

    public IMotivoDeDecisaoDoProcessoRepository MotivosEscolhidos { get; } = MotivoDeTeste.SemEscolha();

    public IDocumentoAPublicarRepository Documentos { get; private set; } = Substitute.For<IDocumentoAPublicarRepository>();

    public IAtoDoProcessoRepository Atos { get; private set; } = Substitute.For<IAtoDoProcessoRepository>();

    public ICertameDivulgadoRepository Certames { get; } = Substitute.For<ICertameDivulgadoRepository>();

    public ISnapshotPublicacaoCanonicalizer Canonicalizer { get; private set; } = CanonicalizerSubstituto();

    public IUnitOfWork UnitOfWork { get; private set; } = Substitute.For<IUnitOfWork>();

    public IUserContext Usuario { get; private set; } = UsuarioAutenticado();

    public ITipoAtoPublicadoReader TiposDeAto { get; private set; } = TipoDeAtoReader();

    public IRegistroDeAtoNormativo Registro { get; private set; } = RegistroQueAceita();

    public IObrigatoriedadeLegalRepository ObrigatoriedadesLegais { get; private set; } = Substitute.For<IObrigatoriedadeLegalRepository>();

    public IModalidadeReader Modalidades { get; private set; } = CadastrosVivos.Modalidades();

    public ITipoDocumentoReader TiposDocumento { get; private set; } = CadastrosVivos.TiposDocumento();

    public IFatoCandidatoReader CatalogoDeFatos { get; private set; } = CadastrosVivos.CatalogoDeFatos();

    public ICalendarioVigenteReader Calendario { get; private set; } = CalendarioVigenteReaderDeTeste.SemVigente();

    public IResolvedorDaUnidadeResponsavel Unidades { get; private set; } = ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste();

    public IRegraCatalogoReader RegrasDeConsolidacao { get; private set; } = RegrasDeConsolidacaoDeTeste.Catalogo();

    public IPrecedenciaFaseReader Precedencias { get; private set; } = PrecedenciasDeTeste.SemArestas();

    public IRegistroCodecsEnvelope Codecs { get; private set; } = CodecsDeTeste.QueReidrataSemTiposDeAto();

    public TimeProvider Relogio { get; private set; } = new RelogioFixo(Agora);

    private ITipoEtapaReader TiposEtapa { get; } = CadastrosVivos.TiposEtapa();

    private ITipoDeficienciaReader TiposDeficiencia { get; } = CadastrosVivos.TiposDeficiencia();

    private IRegraCatalogoReader RegrasDesempate { get; } = CadastrosVivos.RegrasDesempate();

    private IResolvedorFusoInstitucional Fuso { get; } = new ResolvedorFusoDeTeste();

    private IResolvedorFusoInstitucional? FusoDaConferencia { get; set; }

    /// <summary>Os serviços de um processo que o repositório devolve para mutação, com o documento do edital confirmado.</summary>
    public static ServicosDoCongelamentoDeTeste Para(ProcessoSeletivo processo) =>
        new ServicosDoCongelamentoDeTeste().ComProcesso(processo);

    public ServicosDoCongelamentoDeTeste ComProcesso(ProcessoSeletivo processo)
    {
        Repositorio = RepositorioDoProcesso(processo);
        Documentos = RepositorioDeDocumento(processo.Id);

        return ComMotivosDeDecisaoDoProcesso(processo);
    }

    // O processo escolhe, no rascunho, os motivos das decisões que declara; a porta devolve essa escolha, inclusive depois da publicação.
    public ServicosDoCongelamentoDeTeste ComMotivosDeDecisaoDoProcesso(ProcessoSeletivo processo)
    {
        _ = MotivosDeDecisaoDoProcessoDeTeste.Escolha(processo);
        MotivosEscolhidos.ListarEscolhaAtualComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => MotivosDeDecisaoDoProcessoDeTeste.Escolha(processo));
        MotivosEscolhidos.ListarEscolhaAtualAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_ => [.. MotivosDeDecisaoDoProcessoDeTeste.Escolha(processo).Select(static m => m.Escolhido)]);

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComRepositorio(IProcessoSeletivoRepository repositorio)
    {
        Repositorio = repositorio;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComDocumentos(IDocumentoAPublicarRepository documentos)
    {
        Documentos = documentos;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComAtos(IAtoDoProcessoRepository atos)
    {
        Atos = atos;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComCanonicalizer(ISnapshotPublicacaoCanonicalizer canonicalizer)
    {
        Canonicalizer = canonicalizer;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComUnitOfWork(IUnitOfWork unitOfWork)
    {
        UnitOfWork = unitOfWork;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComUsuario(IUserContext usuario)
    {
        Usuario = usuario;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComTiposDeAto(ITipoAtoPublicadoReader tiposDeAto)
    {
        TiposDeAto = tiposDeAto;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComRegistroDeAtos(IRegistroDeAtoNormativo registro)
    {
        Registro = registro;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComObrigatoriedadesLegais(IObrigatoriedadeLegalRepository obrigatoriedadesLegais)
    {
        ObrigatoriedadesLegais = obrigatoriedadesLegais;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComModalidades(IModalidadeReader modalidades)
    {
        Modalidades = modalidades;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComTiposDocumento(ITipoDocumentoReader tiposDocumento)
    {
        TiposDocumento = tiposDocumento;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComCatalogoDeFatos(IFatoCandidatoReader catalogoDeFatos)
    {
        CatalogoDeFatos = catalogoDeFatos;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComCalendario(ICalendarioVigenteReader calendario)
    {
        Calendario = calendario;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComUnidades(IResolvedorDaUnidadeResponsavel unidades)
    {
        Unidades = unidades;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComRegrasDeConsolidacao(IRegraCatalogoReader regras)
    {
        RegrasDeConsolidacao = regras;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComPrecedencias(IPrecedenciaFaseReader precedencias)
    {
        Precedencias = precedencias;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComCodecs(IRegistroCodecsEnvelope codecs)
    {
        Codecs = codecs;

        return this;
    }

    /// <summary>A versão vigente que o repositório devolve ao processo, para a retificação.</summary>
    public ServicosDoCongelamentoDeTeste ComVersaoVigente(ProcessoSeletivo processo, VersaoConfiguracao versaoVigente)
    {
        ArgumentNullException.ThrowIfNull(processo);
        Repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoVigente);

        return this;
    }

    /// <summary>O fuso que só a conferência da configuração resolve; o instante da publicação segue lido no fuso do ambiente.</summary>
    public ServicosDoCongelamentoDeTeste ComFusoDaConferencia(IResolvedorFusoInstitucional fuso)
    {
        FusoDaConferencia = fuso;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComRelogio(TimeProvider relogio)
    {
        Relogio = relogio;

        return this;
    }

    public LeituraDoInstanteDoComando LeituraDoInstante() => new(Relogio, Fuso);

    public ConferenciaDoAtoEDoDocumento AtoEDocumento() => new(Documentos, Repositorio, new ConferenciaDoTipoDeAto(TiposDeAto), LeituraDoInstante());

    public LeituraDaBaseDaRetificacao LeituraDaBase() => new(Repositorio, Atos, Codecs);

    public LeituraDasVersoesPublicadas VersoesPublicadas() => new(Repositorio, Codecs);

    public ConferenciaDoCatalogoEDaProtecao Catalogos() => new(CatalogoDeFatos, TiposDocumento, Substitute.For<IFaseCanonicaReader>());

    public RecopiaAoCongelar Recopia() => new(Unidades, VersoesPublicadas());

    public ResolucaoDosTiposDeAtoUnicos TiposDeAtoUnicos() => new(TiposDeAto, Atos, Relogio);

    public ConferenciaDosAnexosDoEdital Anexos() => new(Documentos);

    public ConferenciaDeConformidadeLegal ConformidadeLegal() =>
        new(ObrigatoriedadesLegais, Modalidades, TiposDocumento, TiposEtapa, TiposDeficiencia, RegrasDesempate);

    public LeituraDosCadastrosDaConfiguracao CadastrosDaConfiguracao() => new(TiposDeAtoUnicos(), Contextos(), Corretores(), new LeituraDasPrecedenciasDosCadastros(Precedencias), MotivosEscolhidos);

    public LeituraDoContextoDeContagemDePrazos Contextos() => new(Calendario, Fuso);

    public LeituraDasRegrasCitadasNaConfiguracao Corretores() => new(RegrasDeConsolidacao);

    public ConferenciaDosDocumentosDaOperacao DocumentosDaOperacao() => new(Anexos(), ConformidadeLegal());

    public ConferenciaDaOperacaoAoCongelar Operacao() => new(DocumentosDaOperacao());

    public ConferenciaDaConfiguracaoAoCongelar Conferencia() =>
        new(Catalogos(), Recopia(), FusoDaConferencia ?? Fuso, CadastrosDaConfiguracao(), Operacao());

    public MontagemDoCongelamento Montagem() => new(Canonicalizer, Usuario);

    public MontagemDaDivulgacao Divulgacao() => new(Certames, EnderecoNoAcervo());

    public GravacaoDaVersaoCongelada Gravacao() => new(Repositorio, Atos, Registro, Divulgacao(), UnitOfWork);

    public CongelamentoDaVersao Congelamento() => new(AtoEDocumento(), Conferencia(), Montagem(), Gravacao(), UnitOfWork);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(PublicarProcessoSeletivoCommand comando) =>
        PublicarProcessoSeletivoCommandHandler.Handle(comando, Repositorio, Congelamento(), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(RetificarProcessoSeletivoCommand comando) =>
        RetificarProcessoSeletivoCommandHandler.Handle(
            comando, Repositorio, Congelamento(), LeituraDaBase(), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(Guid processoId) =>
        PublicarAsync(new PublicarProcessoSeletivoCommand(
            processoId, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: PublicacaoDeTeste.NovoAto()));

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(Guid processoId) =>
        RetificarAsync(new RetificarProcessoSeletivoCommand(
            processoId, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: PublicacaoDeTeste.NovoAto()));

    public AvaliacaoDaConformidade Avaliacao() => new(
        new LeituraDaVersaoPublicadaDoPainel(Repositorio, Codecs),
        new LeituraDosCadastrosDoPainel(
            Repositorio, Contextos(), Corretores(), TiposDeAtoUnicos(), new LeituraDasPrecedenciasDosCadastros(Precedencias), MotivosEscolhidos),
        Catalogos());

    public Task<ConformidadeProcessoSeletivoDto?> ConformidadeAsync(ObterConformidadeProcessoSeletivoQuery consulta) =>
        ObterConformidadeProcessoSeletivoQueryHandler.Handle(consulta, Repositorio, Avaliacao(), CancellationToken.None);
}
