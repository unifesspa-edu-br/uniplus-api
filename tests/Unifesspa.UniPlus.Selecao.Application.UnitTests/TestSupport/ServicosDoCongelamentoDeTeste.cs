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

    public IProcessoSeletivoRepository Repositorio { get; private set; } = Substitute.For<IProcessoSeletivoRepository>();

    public IDocumentoAPublicarRepository Documentos { get; private set; } = Substitute.For<IDocumentoAPublicarRepository>();

    public IRascunhoDePublicacaoRepository Rascunhos { get; } = Substitute.For<IRascunhoDePublicacaoRepository>();

    public IAtoDoProcessoRepository Atos { get; private set; } = Substitute.For<IAtoDoProcessoRepository>();

    public ICertameDivulgadoRepository Certames { get; } = Substitute.For<ICertameDivulgadoRepository>();

    public ISnapshotPublicacaoCanonicalizer Canonicalizer { get; private set; } = CanonicalizerSubstituto();

    public IUnitOfWork UnitOfWork { get; private set; } = Substitute.For<IUnitOfWork>();

    public IUserContext Usuario { get; private set; } = UsuarioAutenticado();

    public ITipoAtoPublicadoReader TiposDeAto { get; private set; } = TipoDeAtoReader();

    public IVagaDeLinhagemReader Vagas { get; private set; } = Substitute.For<IVagaDeLinhagemReader>();

    public IObrigatoriedadeLegalRepository ObrigatoriedadesLegais { get; private set; } = Substitute.For<IObrigatoriedadeLegalRepository>();

    public IModalidadeReader Modalidades { get; private set; } = CadastrosVivos.Modalidades();

    public ITipoDocumentoReader TiposDocumento { get; private set; } = CadastrosVivos.TiposDocumento();

    public IFatoCandidatoReader CatalogoDeFatos { get; private set; } = CadastrosVivos.CatalogoDeFatos();

    public ICalendarioVigenteReader Calendario { get; private set; } = CalendarioVigenteReaderDeTeste.SemVigente();

    public IResolvedorDaUnidadeResponsavel Unidades { get; private set; } = ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste();

    public IRegistroCodecsEnvelope Codecs { get; private set; } = CodecsDeTeste.QueReidrataSemTiposDeAto();

    public IFasesIniciadasDoProcesso FasesIniciadas { get; private set; } = FasesIniciadasDeTeste.Nenhuma();

    public TimeProvider Relogio { get; private set; } = new RelogioFixo(Agora);

    private ITipoEtapaReader TiposEtapa { get; } = CadastrosVivos.TiposEtapa();

    private ITipoDeficienciaReader TiposDeficiencia { get; } = CadastrosVivos.TiposDeficiencia();

    private IRegraCatalogoReader RegrasDesempate { get; } = CadastrosVivos.RegrasDesempate();

    private IResolvedorFusoInstitucional Fuso { get; } = new ResolvedorFusoDeTeste();

    /// <summary>Os serviços de um processo que o repositório devolve para mutação, com o documento do edital confirmado.</summary>
    public static ServicosDoCongelamentoDeTeste Para(ProcessoSeletivo processo) =>
        new ServicosDoCongelamentoDeTeste().ComProcesso(processo);

    public ServicosDoCongelamentoDeTeste ComProcesso(ProcessoSeletivo processo)
    {
        Repositorio = RepositorioDoProcesso(processo);
        Documentos = RepositorioDeDocumento(processo.Id);

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

    public ServicosDoCongelamentoDeTeste ComVagasDeLinhagem(IVagaDeLinhagemReader vagas)
    {
        Vagas = vagas;

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

    public ServicosDoCongelamentoDeTeste ComCodecs(IRegistroCodecsEnvelope codecs)
    {
        Codecs = codecs;

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComFasesIniciadas(IFasesIniciadasDoProcesso fasesIniciadas)
    {
        FasesIniciadas = fasesIniciadas;

        return this;
    }

    /// <summary>A versão vigente que o repositório devolve ao processo, para a retificação e o fechamento da sessão.</summary>
    public ServicosDoCongelamentoDeTeste ComVersaoVigente(ProcessoSeletivo processo, VersaoConfiguracao versaoVigente)
    {
        ArgumentNullException.ThrowIfNull(processo);
        Repositorio.ObterVersaoAtualAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(versaoVigente);

        return this;
    }

    public ServicosDoCongelamentoDeTeste ComRelogio(TimeProvider relogio)
    {
        Relogio = relogio;

        return this;
    }

    public LeituraDoInstanteDaPublicacao LeituraDoInstante() => new(Relogio, Fuso);

    public ConferenciaDoAtoEDoDocumento AtoEDocumento() => new(Documentos, Repositorio, TiposDeAto, Vagas, LeituraDoInstante());

    public LeituraDaBaseDaRetificacao LeituraDaBase() => new(Repositorio, Vagas, Atos, Codecs, FasesIniciadas);

    public LeituraDasVersoesPublicadas VersoesPublicadas() => new(Repositorio, Codecs);

    public ConferenciaDoCatalogoEDaProtecao Catalogos() => new(CatalogoDeFatos, TiposDocumento);

    public RecopiaAoCongelar Recopia() => new(Unidades, VersoesPublicadas());

    public ResolucaoDosTiposDeAtoUnicos TiposDeAtoUnicos() => new(TiposDeAto, Atos, Relogio);

    public ConferenciaDosAnexosDoEdital Anexos() => new(Documentos);

    public ConferenciaDeConformidadeLegal ConformidadeLegal() =>
        new(ObrigatoriedadesLegais, Modalidades, TiposDocumento, TiposEtapa, TiposDeficiencia, RegrasDesempate);

    public ConferenciaDosMotivosAoCongelar Motivos() => new(Repositorio);

    public LeituraDoContextoDeContagemDePrazos Contextos() => new(Calendario, Fuso);

    public ConferenciaDaConfiguracaoAoCongelar Conferencia() =>
        new(Catalogos(), Recopia(), Fuso, TiposDeAtoUnicos(), Anexos(), ConformidadeLegal(), Motivos());

    public MontagemDoCongelamento Montagem() => new(Contextos(), Canonicalizer, Usuario);

    public GravacaoDaDivulgacao Divulgacao() => new(Certames, EnderecoNoAcervo());

    public GravacaoDaVersaoCongelada Gravacao() => new(Repositorio, Atos, Rascunhos, Divulgacao(), UnitOfWork);

    public CongelamentoDaVersao Congelamento() => new(Conferencia(), Montagem(), Gravacao(), UnitOfWork);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(PublicarProcessoSeletivoCommand comando) =>
        PublicarProcessoSeletivoCommandHandler.Handle(comando, Repositorio, AtoEDocumento(), Congelamento(), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(RetificarProcessoSeletivoCommand comando) =>
        RetificarProcessoSeletivoCommandHandler.Handle(
            comando, Repositorio, AtoEDocumento(), Congelamento(), LeituraDaBase(), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> FecharRetificacaoAsync(FecharRetificacaoCommand comando) =>
        FecharRetificacaoCommandHandler.Handle(
            comando, Repositorio, AtoEDocumento(), Congelamento(), LeituraDaBase(), CancellationToken.None);

    public Task<(Result Resposta, IEnumerable<object> Eventos)> PublicarAsync(Guid processoId) =>
        PublicarAsync(new PublicarProcessoSeletivoCommand(
            processoId, "001/2026", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: PublicacaoDeTeste.NovoAto()));

    public Task<(Result Resposta, IEnumerable<object> Eventos)> RetificarAsync(Guid processoId) =>
        RetificarAsync(new RetificarProcessoSeletivoCommand(
            processoId, "Correção do prazo", "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: PublicacaoDeTeste.NovoAto()));

    public Task<(Result Resposta, IEnumerable<object> Eventos)> FecharRetificacaoAsync(Guid processoId) =>
        FecharRetificacaoAsync(new FecharRetificacaoCommand(
            processoId, "001/2026-R1", null, null, DocumentoEditalId: Guid.CreateVersion7(), Ato: PublicacaoDeTeste.NovoAto(), Precondicao: PrecondicaoIfMatch.Curinga));

    public Task<ConformidadeProcessoSeletivoDto?> ConformidadeAsync(ObterConformidadeProcessoSeletivoQuery consulta) =>
        ObterConformidadeProcessoSeletivoQueryHandler.Handle(
            consulta, Repositorio, Contextos(), TiposDeAtoUnicos(), Catalogos(), Codecs, CancellationToken.None);
}
