namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class CenarioDaPublicacaoDoProdutoDoEdital
{
    public CenarioDaPublicacaoDoProdutoDoEdital()
    {
        Processos.ObterSiglaDaUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        Processos.ObterAtosCriadoresAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([VersaoDivulgadaAtoId]);
        ConfigurarCertameDivulgado();
        Atos.ObterComAnexosAsync(VersaoDivulgadaAtoId, Arg.Any<CancellationToken>()).Returns(RegistroDoAto(VersaoDivulgadaAtoId, "EDITAL_ABERTURA"));
        Atos.ListarDoProcessoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(_ => AtosDoProcesso.ToList());
        Atos.ListarDocumentosJaUsadosAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());
        Documentos.ListarDoProcessoAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1]).Where(id => id == Principal.Id).Select(_ => Principal).ToList());
        Codecs.LeAVersao(Versao).Returns(true);
        Usuario.UserId.Returns(ContaId);
        Tipo(Materia, resultado: true);
    }

    private void ConfigurarCertameDivulgado() =>
        Divulgados.ObterParaMutacaoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                ProcessoId, 1, VersaoDivulgadaAtoId, new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2027", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));

    public const string Materia = "RESULTADO_HOMOLOGACAO";
    public const string Versao = "0.0.10";
    public const string ContaId = "conta-do-administrador";
    public static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly DataDoAto = new(2027, 1, 20);
    public static readonly Guid ProcessoDosDocumentos = Guid.CreateVersion7();

    public Guid ProcessoId { get; } = Guid.CreateVersion7();
    public Guid VersaoDivulgadaAtoId { get; } = Guid.CreateVersion7();

    public IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();
    public ICertameDivulgadoRepository Divulgados { get; } = Substitute.For<ICertameDivulgadoRepository>();
    public IAtoDoProcessoRepository Atos { get; } = Substitute.For<IAtoDoProcessoRepository>();
    public IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();
    public IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();
    public ITipoAtoPublicadoReader Tipos { get; } = Substitute.For<ITipoAtoPublicadoReader>();
    public IRegistroDeAtoNormativo Registro { get; } = ServicosDoCongelamentoDeTeste.RegistroQueAceita();
    public IUserContext Usuario { get; } = Substitute.For<IUserContext>();
    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    // O que a versão publicada congelou do tipo do item; o catálogo vivo (Tipos) só diz que o tipo existe.
    public TipoDeAtoCongelado Congelado { get; set; } = new(Materia, Nome: "Nome do ato", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);

    public DocumentoAPublicar Principal { get; } = Documento();
    public List<AtoDoProcesso> AtosDoProcesso { get; } = [];
    public Dictionary<Guid, IdentidadeDoItemDoEdital> ItensDoCronograma { get; private set; } = [];

    public async Task DeveRecusar(PublicarProdutoDoEditalCommand comando, string codigoEsperado)
    {
        Atos.ClearReceivedCalls();
        UnitOfWork.ClearReceivedCalls();

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(comando);

        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle();
        resposta.Error!.Code.Should().Be(codigoEsperado);
        mensagens.Should().BeEmpty("a recusa acontece antes de qualquer envio ao registro central");
        await Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    public Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Publicar(PublicarProdutoDoEditalCommand comando, TimeProvider? relogio = null) =>
        PublicarProdutoDoEditalCommandHandler.Handle(
            comando, Processos,
            new PreparacaoDaPublicacaoDoProdutoDoEdital(
                new ConferenciaDoProcessoDivulgado(Processos, Divulgados),
                new LeituraDoInstanteDoComando(relogio ?? new RelogioFixo(Agora), new ResolvedorFusoDeTeste()),
                new ConferenciaDoProdutoDoEdital(new ConfiguracaoVigenteDoProcesso(Processos, Codecs),
                    new ConferenciaDoTipoDeAto(Tipos), Atos, new ConferenciaDoAtoCorrigido(Atos, Processos), Usuario),
                new ConferenciaDosDocumentosDoAto(Documentos, Atos)),
            new PublicacaoDoAtoDoProcesso(Atos, Registro, UnitOfWork), CancellationToken.None);

    public PublicarProdutoDoEditalCommand Comando(Guid produtoId, bool politica = true, Guid? corrige = null) =>
        new(ProcessoId, produtoId, "5/2027", DataDoAto, "Diretor do CEPS", Principal.Id,
            PoliticaDeDivulgacaoConfirmada: politica, AtoCorrigidoId: corrige, MotivoDaCorrecao: corrige is null ? null : "Erro de digitação");

    /// <summary>O tipo no catálogo vivo, que a publicação só consulta para saber que ele existe e vigora.</summary>
    public void Tipo(string codigo, bool resultado, bool unico = false, bool congela = false) =>
        Tipos.ObterVigenteAsync(codigo, DataDoAto, Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(codigo, "Resultado da homologação", CongelaConfiguracao: congela, UnicoPorObjeto: unico, EfeitoIrreversivel: false, EhResultado: resultado));

    public void Cronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2027 — Medicina");
        ItensDoCronograma = IdentidadeDoItemDoEdital.Dos(fases, [.. processo.Etapas]);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, new DocumentoCongelado([1], Versao, "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "sub", Agora);
        Processos.ObterVersaoVigenteAsync(ProcessoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        Codecs.Reidratar(versao).Returns(_ => Result<EnvelopeReidratado>.Success(Envelope(processo, fases)));
    }

    private EnvelopeReidratado Envelope(ProcessoSeletivo processo, IReadOnlyList<FaseCronograma> fases) =>
        new(new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, fases),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.BonusRegional is not null,
                processo.BonusRegional, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada([], [], [], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null),
            DadosEdital.Criar("001/2027", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(TiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal) { [Materia] = Congelado }));

    public static FaseCronograma Fase(string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false)),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: Agora, Fim: Agora.AddDays(10), UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    public static (ProdutoDaFase Preliminar, ProdutoDaFase Definitivo) Par() =>
        (ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo));

    public (ProdutoDaFase Preliminar, ProdutoDaFase Definitivo) ParNaMesmaFase()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar, definitivo));
        return (preliminar, definitivo);
    }

    /// <summary>Um ato já publicado do item, registrado na Seleção e conhecido como ato do processo.</summary>
    public AtoDoProcesso Publicado(string tipo, Guid produtoId)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), ProcessoId, tipo, tipo, "1/2027", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
                Guid.CreateVersion7(), [], ItemDoEdital: ItensDoCronograma[produtoId]),
            Agora).Value!;
        typeof(AtoDoProcesso).GetProperty(nameof(AtoDoProcesso.CreatedAt))!.SetValue(ato, Agora.AddMinutes(-60 + AtosDoProcesso.Count));
        AtosDoProcesso.Add(ato);
        Atos.ObterProcessoDoAtoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(ProcessoId);
        Atos.ListarCadeiaDeCorrecaoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns([ato.Id]);
        return ato;
    }

    private AtoDoProcesso RegistroDoAto(Guid atoId, string tipo) => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            atoId, ProcessoId, tipo, tipo, "1/2027", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
            Guid.CreateVersion7(), []),
        Agora).Value!;

    private static DocumentoAPublicar Documento()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoDosDocumentos, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }
}
