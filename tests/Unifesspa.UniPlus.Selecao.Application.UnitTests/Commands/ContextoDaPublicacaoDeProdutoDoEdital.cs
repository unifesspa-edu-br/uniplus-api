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
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public abstract class ContextoDaPublicacaoDeProdutoDoEdital
{
    protected ContextoDaPublicacaoDeProdutoDoEdital()
    {
        Processos.ObterSiglaDaUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        Processos.ObterAtosCriadoresAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([VersaoDivulgadaAtoId]);
        Divulgados.ObterParaMutacaoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                ProcessoId, 1, VersaoDivulgadaAtoId, new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2027", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));
        Atos.ObterComAnexosAsync(VersaoDivulgadaAtoId, Arg.Any<CancellationToken>()).Returns(RegistroDoAto(VersaoDivulgadaAtoId, "EDITAL_ABERTURA"));
        Atos.ListarDoProcessoAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(_ => AtosDoProcesso.ToList());
        Atos.ListarDocumentosJaUsadosAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());
        Documentos.ListarDoProcessoAsync(ProcessoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1]).Where(id => id == Principal.Id).Select(_ => Principal).ToList());
        Codecs.Capacidades.Returns([new CapacidadeCodec(Versao, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        Usuario.UserId.Returns(ContaId);
        Tipo(Materia, resultado: true);
    }

    protected const string Materia = "RESULTADO_HOMOLOGACAO";
    protected const string Versao = "0.0.10";
    protected const string ContaId = "conta-do-administrador";
    protected static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);
    protected static readonly DateOnly DataDoAto = new(2027, 1, 20);
    protected static readonly Guid ProcessoDosDocumentos = Guid.CreateVersion7();

    protected Guid ProcessoId { get; } = Guid.CreateVersion7();
    protected Guid VersaoDivulgadaAtoId { get; } = Guid.CreateVersion7();

    protected IProcessoSeletivoRepository Processos { get; } = Substitute.For<IProcessoSeletivoRepository>();
    protected ICertameDivulgadoRepository Divulgados { get; } = Substitute.For<ICertameDivulgadoRepository>();
    protected IAtoDoProcessoRepository Atos { get; } = Substitute.For<IAtoDoProcessoRepository>();
    protected IDocumentoAPublicarRepository Documentos { get; } = Substitute.For<IDocumentoAPublicarRepository>();
    protected IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();
    protected ITipoAtoPublicadoReader Tipos { get; } = Substitute.For<ITipoAtoPublicadoReader>();
    protected IRegistroDeAtoNormativo Registro { get; } = ServicosDoCongelamentoDeTeste.RegistroQueAceita();
    protected IUserContext Usuario { get; } = Substitute.For<IUserContext>();
    protected IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    // O que a versão publicada congelou do tipo do item; o catálogo vivo (Tipos) só diz que o tipo existe.
    protected TipoDeAtoCongelado Congelado { get; set; } = new(Materia, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);

    protected DocumentoAPublicar Principal { get; } = Documento();
    protected List<AtoDoProcesso> AtosDoProcesso { get; } = [];
    protected Dictionary<Guid, IdentidadeDoItemDoEdital> ItensDoCronograma { get; set; } = [];

    protected async Task DeveRecusar(PublicarProdutoDoEditalCommand comando, string codigoEsperado)
    {
        Atos.ClearReceivedCalls();
        UnitOfWork.ClearReceivedCalls();

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(comando);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(codigoEsperado);
        mensagens.Should().BeEmpty("a recusa acontece antes de qualquer envio ao registro central");
        await Atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    protected Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Publicar(PublicarProdutoDoEditalCommand comando, TimeProvider? relogio = null) =>
        PublicarProdutoDoEditalCommandHandler.Handle(
            comando, Processos, Divulgados, Atos, Documentos, Codecs, Tipos, Registro, Usuario, UnitOfWork,
            new LeituraDoInstanteDaPublicacao(relogio ?? new RelogioFixo(Agora), new ResolvedorFusoDeTeste()), CancellationToken.None);

    protected PublicarProdutoDoEditalCommand Comando(Guid produtoId, bool politica = true, Guid? corrige = null) =>
        new(ProcessoId, produtoId, "5/2027", DataDoAto, "Diretor do CEPS", Principal.Id,
            PoliticaDeDivulgacaoConfirmada: politica, AtoCorrigidoId: corrige, MotivoDaCorrecao: corrige is null ? null : "Erro de digitação");

    /// <summary>O tipo no catálogo vivo, que a publicação só consulta para saber que ele existe e vigora.</summary>
    protected void Tipo(string codigo, bool resultado, bool unico = false, bool congela = false) =>
        Tipos.ObterVigenteAsync(codigo, DataDoAto, Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(codigo, "Resultado da homologação", CongelaConfiguracao: congela, UnicoPorObjeto: unico, EfeitoIrreversivel: false, EhResultado: resultado));

    protected void Cronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2027 — Medicina");
        ItensDoCronograma = IdentidadeDoItemDoEdital.Dos(fases, [.. processo.Etapas]);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, [1], Versao, "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", Agora);
        Processos.ObterVersaoVigenteAsync(ProcessoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        Codecs.Reidratar(versao).Returns(_ => Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.AtendimentoEspecializado!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                fases, [.. processo.DocumentosExigidos], [], null, numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
            tiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal) { [Materia] = Congelado })));
    }

    protected static FaseCronograma Fase(string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseQueConclui(codigo, ordem, null, produtos);

    protected static FaseCronograma FaseQueConclui(string codigo, int ordem, string? faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(
                Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false, ColetaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: Agora, Fim: Agora.AddDays(10), faseConcluinteCodigo, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    protected static (ProdutoDaFase Preliminar, ProdutoDaFase Definitivo) Par() =>
        (ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo));

    /// <summary>Um ato já publicado do item, registrado na Seleção e conhecido como ato do processo.</summary>
    protected AtoDoProcesso Publicado(string tipo, Guid produtoId)
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

    protected AtoDoProcesso RegistroDoAto(Guid atoId, string tipo) => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            atoId, ProcessoId, tipo, tipo, "1/2027", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
            Guid.CreateVersion7(), []),
        Agora).Value!;

    protected static DocumentoAPublicar Documento()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoDosDocumentos, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }
}
