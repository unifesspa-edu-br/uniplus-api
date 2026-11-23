namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cobertura do congelamento de metadado de fato (Story #919, congelamento dos parâmetros na publicação, UNI-REQ-0019) em
/// <see cref="PublicarProcessoSeletivoCommandHandler"/>: o resultado da resolução alimenta
/// <see cref="EntradaCanonicalizacao.MetadadosFatosCongelados"/>, e um código de fato que não
/// resolve no catálogo aborta a publicação com um erro nomeado ANTES de canonicalizar.
/// </summary>
/// <remarks>
/// Desde a issue #1059 (D4-bis), <see cref="IFatoCandidatoReader.ListarAsync"/> é consultado
/// UMA vez por congelamento, sempre — mesmo sem condição de gatilho — porque a verificação de valor
/// inativo do domínio precisa do catálogo inteiro independente de o processo ter fato coletado
/// ou gatilho. É esse catálogo compartilhado, não mais <c>ObterPorCodigoAsync</c> por código,
/// que <see cref="ResolvedorMetadadosFatosCongelados"/> agora consulta.
/// </remarks>
public sealed partial class PublicarProcessoSeletivoCommandHandlerTests
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];

    private static ProcessoSeletivo NovoProcessoConforme(out Guid faseId) =>
        ProcessoSeletivoConformeBuilder.Criar("PS Metadado de Fato", out faseId);

    private static DocumentoExigido ExigenciaComGatilhoPorFato(Guid faseId, string fato) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(CadastrosVivos.IdentidadeDe("LAUDO_MEDICO"), "DECLARACAO", "Declaração", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(faseId, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(true, null, [DocumentoExigidoBaseLegal.Criar(
                "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!]))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, new PrazoDeEmissaoBuilder().SemPrazo().Construir(), new ValidadeDoDocumentoBuilder().NaoExige().Construir()))
            .ComFatos(null)
            .Criar().Value!;

    private static FatoCandidatoView FatoModalidade() => new(
        Id: Guid.CreateVersion7(),
        Codigo: "MODALIDADE",
        Nome: "Modalidade de concorrência",
        Descricao: "Descrição do fato.",
        Dominio: "CATEGORICO",
        Origem: "DERIVADO",
        Cardinalidade: "MULTIVALORADO",
        ValoresDominio: null,
        PontoResolucao: "INSCRICAO",
        Binding: "REGRA_DERIVACAO:MODALIDADE",
        ValoresDominioDeclarados: null, FonteValores: "MODALIDADE", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);

    private sealed record Mocks(
        IProcessoSeletivoRepository ProcessoRepository,
        IDocumentoAPublicarRepository DocumentoRepository,
        ISnapshotPublicacaoCanonicalizer Canonicalizer,
        ITipoAtoPublicadoReader TipoDeAtoReader,
        IObrigatoriedadeLegalRepository ObrigatoriedadeLegalRepository,
        IFatoCandidatoReader FatoCandidatoReader);

    private static (Mocks Mocks, DocumentoAPublicar Documento) NovosMocks(ProcessoSeletivo processo, Action<EntradaCanonicalizacao>? captura = null)
    {
        IProcessoSeletivoRepository processoRepository = RepositorioDoProcesso(processo);
        DocumentoAPublicar documento = DocumentoDeTeste(processo);
        IDocumentoAPublicarRepository documentoRepository = RepositorioDoDocumento(documento);

        return (new Mocks(
            processoRepository,
            documentoRepository,
            CanonicalizerDeTeste(captura),
            TipoDeAtoDeTeste(),
            ObrigatoriedadesDeTeste(),
            CatalogoDeTeste()), documento);
    }

    private static IProcessoSeletivoRepository RepositorioDoProcesso(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static DocumentoAPublicar DocumentoDeTeste(ProcessoSeletivo processo)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processo.Id, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static IDocumentoAPublicarRepository RepositorioDoDocumento(DocumentoAPublicar documento)
    {
        IDocumentoAPublicarRepository repository = Substitute.For<IDocumentoAPublicarRepository>();
        repository.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>()).Returns(documento);
        return repository;
    }

    private static ISnapshotPublicacaoCanonicalizer CanonicalizerDeTeste(Action<EntradaCanonicalizacao>? captura)
    {
        ISnapshotPublicacaoCanonicalizer canonicalizer = Substitute.For<ISnapshotPublicacaoCanonicalizer>();
        canonicalizer.Canonicalizar(Arg.Do<EntradaCanonicalizacao>(e => captura?.Invoke(e)))
            .Returns(new SnapshotCanonico(EnvelopeProjetavelDeTeste.Bytes, "1.3", "canonical-json/sha256@v1"));
        return canonicalizer;
    }

    private static ITipoAtoPublicadoReader TipoDeAtoDeTeste()
    {
        ITipoAtoPublicadoReader reader = Substitute.For<ITipoAtoPublicadoReader>().ComQualquerTipoDeclarado();
        reader.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                Codigo: "EDITAL_ABERTURA", Nome: "Edital de abertura",
                CongelaConfiguracao: true, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
        return reader;
    }

    private static IFatoCandidatoReader CatalogoDeTeste()
    {
        IFatoCandidatoReader reader = Substitute.For<IFatoCandidatoReader>();
        reader.ListarAsync(Arg.Any<CancellationToken>()).Returns(CatalogoDoConjuntoBasico.Fatos);
        return reader;
    }

    private static IObrigatoriedadeLegalRepository ObrigatoriedadesDeTeste()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        repository.ObterVigentesParaTipoProcessoAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        return repository;
    }

    /// <summary>
    /// A leitura do calendário vigente é <b>uma por handler</b>: a mesma resposta alimenta a verificação
    /// da raiz e o bloco congelado do envelope. Duas leituras abririam a janela em que o dataset
    /// muda entre validar e congelar, e a versão publicada carregaria um calendário que a verificação
    /// não aprovou — defeito que nenhuma asserção sobre o resultado pegaria, porque num teste
    /// comum as duas leituras devolveriam o mesmo valor.
    /// </summary>
    [Fact(DisplayName = "O handler lê o calendário vigente uma única vez")]
    public async Task Handle_LeCalendarioUmaUnicaVez()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);
        var reader = CalendarioVigenteReaderDeTeste.ComVigente();

        (Result resposta, IEnumerable<object> _) = await HandleAsync(mocks, processo, documento, reader);

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        reader.Leituras.Should().Be(1);
    }

    [Fact(DisplayName = "A unidade responsável que o cadastro excluiu depois do cronograma recusa a publicação")]
    public async Task Handle_UnidadeExcluidaDepoisDoCronograma_Recusa()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(
            mocks, processo, documento, unidades: ResolvedorDeUnidadeDeTeste.Com());

        resposta.Error!.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        resposta.Errors.Should().ContainSingle().Which.Field.Should().Be("/cronograma/fases/0/unidadeResponsavelCodigo");
        eventos.Should().BeEmpty();
        mocks.Canonicalizer.DidNotReceiveWithAnyArgs().Canonicalizar(default!);
    }

    [Fact(DisplayName = "A unidade responsável renomeada depois do cronograma entra no snapshot com o nome de agora")]
    public async Task Handle_UnidadeRenomeadaDepoisDoCronograma_CongelaONomeNovo()
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        EntradaCanonicalizacao? entrada = null;
        (Mocks mocks, DocumentoAPublicar documento) = NovosMocks(processo, e => entrada = e);
        var renomeada = new UnidadeResponsavelSnapshot(UnidadesDeTeste.Ceps.OrigemId, "CEPS", "Centro de Processos Seletivos e Concursos");

        (Result resposta, IEnumerable<object> _) = await HandleAsync(
            mocks, processo, documento, unidades: ResolvedorDeUnidadeDeTeste.Com(renomeada));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        entrada!.Processo.CronogramaFases.Should().OnlyContain(f => f.UnidadeResponsavelNome == renomeada.Nome);
    }

    [Theory(DisplayName = "O documento do edital que não é PDF é recusado antes de qualquer escrita")]
    [InlineData(TiposDeMidiaDeDocumento.Docx)]
    [InlineData(TiposDeMidiaDeDocumento.Odt)]
    public async Task Handle_DocumentoDoEditalNaoEPdf_Recusa(string tipoDeMidia)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(out _);
        (Mocks mocks, _) = NovosMocks(processo);
        DocumentoAPublicar naoPdf = DocumentoAPublicar.IniciarPendente(
            processo.Id, FinalidadeDoDocumento.AnexoDoEdital, "edital", tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        naoPdf.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        mocks.DocumentoRepository.ObterPorIdAsync(naoPdf.Id, Arg.Any<CancellationToken>()).Returns(naoPdf);

        (Result resposta, IEnumerable<object> eventos) = await HandleAsync(mocks, processo, naoPdf);

        resposta.Error!.Code.Should().Be("ProcessoSeletivo.DocumentoNaoEnviadoComoEdital");
        eventos.Should().BeEmpty();
    }

    private static Task<(Result Resposta, IEnumerable<object> Eventos)> HandleAsync(
        Mocks mocks, ProcessoSeletivo processo, DocumentoAPublicar documento,
        ICalendarioVigenteReader? calendarioReader = null,
        IUnitOfWork? unitOfWork = null,
        IResolvedorDaUnidadeResponsavel? unidades = null,
        ITipoDocumentoReader? tipos = null) =>
        ServicosDoHandler(mocks, calendarioReader, unitOfWork, unidades, tipos)
            .ComMotivosDeDecisaoDoProcesso(processo)
            .PublicarAsync(ComandoDeAbertura(processo, documento));

    private static ServicosDoCongelamentoDeTeste ServicosDoHandler(
        Mocks mocks,
        ICalendarioVigenteReader? calendarioReader,
        IUnitOfWork? unitOfWork,
        IResolvedorDaUnidadeResponsavel? unidades,
        ITipoDocumentoReader? tipos) =>
        ServicosDosMocks(mocks)
            .ComUnitOfWork(unitOfWork ?? Substitute.For<IUnitOfWork>())
            .ComTiposDocumento(tipos ?? CadastrosVivos.TiposDocumento())
            .ComCalendario(calendarioReader ?? CalendarioVigenteReaderDeTeste.SemVigente())
            .ComUnidades(unidades ?? ResolvedorDeUnidadeDeTeste.ComAsUnidadesDeTeste());

    private static ServicosDoCongelamentoDeTeste ServicosDosMocks(Mocks mocks) =>
        new ServicosDoCongelamentoDeTeste()
            .ComRepositorio(mocks.ProcessoRepository)
            .ComDocumentos(mocks.DocumentoRepository)
            .ComCanonicalizer(mocks.Canonicalizer)
            .ComUsuario(UsuarioDoHandler())
            .ComTiposDeAto(mocks.TipoDeAtoReader)
            .ComObrigatoriedadesLegais(mocks.ObrigatoriedadeLegalRepository)
            .ComCatalogoDeFatos(mocks.FatoCandidatoReader)
            .ComRelogio(TimeProvider.System);

    private static IUserContext UsuarioDoHandler()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns("user-conta-1");
        return userContext;
    }

    private static PublicarProcessoSeletivoCommand ComandoDeAbertura(ProcessoSeletivo processo, DocumentoAPublicar documento) =>
        new(processo.Id, "001/2026", null, null,
            DocumentoEditalId: documento.Id,
            Ato: new DadosDoAto(
                Orgao: "CEPS", Serie: "EDITAL", Ano: 2026, DataDoAto: new DateOnly(2026, 1, 1),
                Assinante: "Diretor do CEPS", TipoAtoCodigo: "EDITAL_ABERTURA"));

}
