namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A publicação de um item que o edital declara: o tipo vem do item, o definitivo espera o preliminar
/// da matéria, o item sai uma vez, o resultado exige a política confirmada e a correção só vale para o
/// item dela e enquanto o definitivo do ciclo não saiu.
/// </summary>
public sealed class PublicarProdutoDoEditalCommandHandlerTests
{
    private const string Materia = "RESULTADO_HOMOLOGACAO";
    private const string Versao = "0.0.10";
    private const string Sub = "sub-do-administrador";
    private static readonly DateTimeOffset Agora = new(2027, 1, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly DataDoAto = new(2027, 1, 20);
    private static readonly Guid ProcessoDosDocumentos = Guid.CreateVersion7();

    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly Guid _versaoDivulgadaAtoId = Guid.CreateVersion7();

    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly ICertameDivulgadoRepository _divulgados = Substitute.For<ICertameDivulgadoRepository>();
    private readonly IAtoDoProcessoRepository _atos = Substitute.For<IAtoDoProcessoRepository>();
    private readonly IDocumentoAPublicarRepository _documentos = Substitute.For<IDocumentoAPublicarRepository>();
    private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();
    private readonly ITipoAtoPublicadoReader _tipos = Substitute.For<ITipoAtoPublicadoReader>();
    private readonly IVagaDeLinhagemReader _vagas = Substitute.For<IVagaDeLinhagemReader>();
    private readonly IUserContext _usuario = Substitute.For<IUserContext>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();

    // O que a versão publicada congelou do tipo do item; o catálogo vivo (_tipos) só diz que o tipo existe.
    private TipoDeAtoCongelado _congelado = new(Materia, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);

    private readonly DocumentoAPublicar _principal = Documento();
    private readonly List<AtoDoProcesso> _atosDoProcesso = [];
    private Dictionary<Guid, IdentidadeDoItemDoEdital> _itensDoCronograma = [];

    public PublicarProdutoDoEditalCommandHandlerTests()
    {
        _processos.ObterSiglaDaUnidadeAdministradoraAsync(_processoId, Arg.Any<CancellationToken>()).Returns("CEPS");
        _processos.ObterAtosCriadoresAsync(_processoId, Arg.Any<CancellationToken>()).Returns([_versaoDivulgadaAtoId]);
        _divulgados.ObterParaLeituraAsync(_processoId, Arg.Any<CancellationToken>()).Returns(
            CertameDivulgado.Criar(
                _processoId, 1, _versaoDivulgadaAtoId, new string('a', 64), "1",
                new FacetasDoCertameDivulgado("psm-medicina-2027", "PSM Medicina 2027", "001/2027", [], Agora.AddDays(-1), Agora.AddDays(20)),
                """{"nome":"documento"}""", Agora));
        _atos.ObterComAnexosAsync(_versaoDivulgadaAtoId, Arg.Any<CancellationToken>()).Returns(RegistroDoAto(_versaoDivulgadaAtoId, "EDITAL_ABERTURA"));
        _atos.ListarDoProcessoAsync(_processoId, Arg.Any<CancellationToken>()).Returns(_ => _atosDoProcesso.ToList());
        _atos.ListarDocumentosJaUsadosAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());
        _documentos.ListarDoProcessoAsync(_processoId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => ((IReadOnlyCollection<Guid>)call[1]).Where(id => id == _principal.Id).Select(_ => _principal).ToList());
        _codecs.Capacidades.Returns([new CapacidadeCodec(Versao, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)]);
        _usuario.UserId.Returns(Sub);
        Tipo(Materia, resultado: true);
    }

    [Fact(DisplayName = "O preliminar de um resultado, com a política confirmada, grava o registro com o item e quem confirmou, e envia o ato com o hash do PDF")]
    public async Task Preliminar_ComPoliticaConfirmada_GravaORegistroDoItemEEnviaOAto()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso? gravado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(preliminar.Id));

        resposta.IsSuccess.Should().BeTrue();
        gravado.Should().NotBeNull();
        gravado!.ItemDoEdital.Should().Be(_itensDoCronograma[preliminar.Id].Chave);
        gravado.TipoCodigo.Should().Be(Materia, "o tipo vem do item que o edital congelou");
        gravado.AtoCorrigidoId.Should().BeNull();
        gravado.PoliticaDeDivulgacaoConfirmadaPorSub.Should().Be(Sub);
        gravado.PoliticaDeDivulgacaoConfirmadaEm.Should().Be(Agora);
        gravado.Situacao.Should().Be(SituacaoDoRegistroDoAto.Pendente);
        RegistrarAtoNormativoRequisicao requisicao = mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>().Subject;
        requisicao.AtoId.Should().Be(gravado.Id);
        requisicao.TipoCodigo.Should().Be(Materia);
        requisicao.DocumentoHash.Should().Be(_principal.HashSha256);
        requisicao.AtoRetificadoId.Should().BeNull();
    }

    [Fact(DisplayName = "A correção do item sem motivo é recusada no campo do motivo, antes de travar ou consultar o processo")]
    public async Task CorrecaoSemMotivo_RecusaAntesDeQualquerLeitura()
    {
        PublicarProdutoDoEditalCommand semMotivo = Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with { MotivoDaCorrecao = null };

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(semMotivo);

        resposta.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(e =>
            e.Field == "motivoDaCorrecao" && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
        mensagens.Should().BeEmpty();
        await _processos.DidNotReceive().TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Item que a versão vigente não declara é recusado, orientando a retificar o edital")]
    public async Task Produto_ForaDaVersaoVigente_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));

        await DeveRecusar(Comando(Guid.CreateVersion7()), AtoDoProcessoErrorCodes.ProdutoNaoDeclarado);
    }

    [Fact(DisplayName = "O definitivo antes do preliminar da mesma matéria é recusado")]
    public async Task Definitivo_AntesDoPreliminar_ERecusado()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));

        await DeveRecusar(Comando(definitivo.Id), AtoDoProcessoErrorCodes.PreliminarNaoPublicado);
    }

    [Fact(DisplayName = "O definitivo com o preliminar ainda aguardando o registro é recusado, e com o preliminar registrado é aceito")]
    public async Task Definitivo_ComPreliminarPendenteERegistrado()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        AtoDoProcesso publicadoPreliminar = Publicado(Materia, preliminar.Id, registrado: false);

        await DeveRecusar(Comando(definitivo.Id), AtoDoProcessoErrorCodes.PreliminarAguardaRegistro);

        publicadoPreliminar.ConfirmarRegistro(new RelogioFixo(Agora));
        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(definitivo.Id));
        resposta.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Publicar de novo um item já publicado ou pendente é recusado, cada um com o seu erro")]
    public async Task Produto_JaPublicadoOuPendente_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        Publicado(Materia, preliminar.Id, registrado: false);

        await DeveRecusar(Comando(preliminar.Id), AtoDoProcessoErrorCodes.ProdutoAguardaRegistro);

        _atosDoProcesso.Single().ConfirmarRegistro(new RelogioFixo(Agora));
        await DeveRecusar(Comando(preliminar.Id), AtoDoProcessoErrorCodes.ProdutoJaPublicado);
    }

    [Fact(DisplayName = "O resultado sem a confirmação da política é recusado, e o item que não é resultado dispensa a confirmação e não a registra")]
    public async Task Politica_ExigidaSoDoResultado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));

        await DeveRecusar(Comando(preliminar.Id, politica: false), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);

        _congelado = _congelado with { EhResultado = false };
        AtoDoProcesso? gravado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());
        (Result<AtoPublicadoDto> resposta, _) = await Publicar(Comando(preliminar.Id, politica: false));

        resposta.IsSuccess.Should().BeTrue();
        gravado!.PoliticaDeDivulgacaoConfirmadaPorSub.Should().BeNull();
    }

    [Fact(DisplayName = "A errata de uma lista publicada é aceita, leva o item e a política confirmada, e aponta o ato corrigido")]
    public async Task Errata_DeItemPublicado_EAceita()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id, registrado: true);
        AtoDoProcesso? gravado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoDoProcesso>(a => gravado = a), Arg.Any<CancellationToken>());

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue();
        gravado!.ItemDoEdital.Should().Be(_itensDoCronograma[preliminar.Id].Chave);
        gravado.AtoCorrigidoId.Should().Be(original.Id);
        gravado.PoliticaDeDivulgacaoConfirmadaPorSub.Should().Be(Sub);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>()
            .Which.AtoRetificadoId.Should().Be(original.Id);
    }

    [Fact(DisplayName = "A correção de um resultado sem a confirmação da política é recusada")]
    public async Task Correcao_SemPolitica_ERecusada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id, registrado: true);

        await DeveRecusar(Comando(preliminar.Id, politica: false, corrige: original.Id), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
    }

    [Fact(DisplayName = "A correção que aponta ato de outro item é recusada")]
    public async Task Correcao_DeAtoDeOutroItem_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        AtoDoProcesso doPreliminar = Publicado(Materia, preliminar.Id, registrado: true);
        Publicado(Materia, definitivo.Id, registrado: true);

        await DeveRecusar(Comando(definitivo.Id, corrige: doPreliminar.Id), AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto);
    }

    [Fact(DisplayName = "A correção do preliminar depois do definitivo publicado é recusada, mandando corrigir o definitivo, que continua corrigível")]
    public async Task Correcao_DoPreliminarDepoisDoDefinitivo_ERecusada()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        AtoDoProcesso doPreliminar = Publicado(Materia, preliminar.Id, registrado: true);

        (Result<AtoPublicadoDto> antes, _) = await Publicar(Comando(preliminar.Id, corrige: doPreliminar.Id));
        antes.IsSuccess.Should().BeTrue("antes do definitivo, a correção do preliminar é aceita");

        AtoDoProcesso doDefinitivo = Publicado(Materia, definitivo.Id, registrado: true);

        (Result<AtoPublicadoDto> depois, _) = await Publicar(Comando(preliminar.Id, corrige: doPreliminar.Id));
        depois.Error!.Code.Should().Be(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        depois.Error.Message.Should().Contain("Corrija o resultado definitivo");

        (Result<AtoPublicadoDto> corrigeODefinitivo, _) = await Publicar(Comando(definitivo.Id, corrige: doDefinitivo.Id));
        corrigeODefinitivo.IsSuccess.Should().BeTrue("a correção do definitivo é aceita");
    }

    [Fact(DisplayName = "O definitivo só fecha o ciclo dele: o preliminar do mesmo tipo em outro ciclo, ainda aberto, continua corrigível")]
    public async Task Correcao_DoPreliminarDeOutroCicloAberto_EAceita()
    {
        (ProdutoDaFase preliminarA, _) = Par();
        (ProdutoDaFase preliminarB, _) = Par();
        ProdutoDaFase definitivoC = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        ProdutoDaFase definitivoD = ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo);
        Cronograma(
            FaseQueConclui("ABRE_A", 1, "FECHA_C", preliminarA),
            FaseQueConclui("ABRE_B", 2, "FECHA_D", preliminarB),
            Fase("FECHA_C", 3, definitivoC),
            Fase("FECHA_D", 4, definitivoD));
        AtoDoProcesso doA = Publicado(Materia, preliminarA.Id, registrado: true);
        AtoDoProcesso doB = Publicado(Materia, preliminarB.Id, registrado: true);
        Publicado(Materia, definitivoC.Id, registrado: true);

        await DeveRecusar(Comando(preliminarA.Id, corrige: doA.Id), AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo);
        (Result<AtoPublicadoDto> outroCiclo, _) = await Publicar(Comando(preliminarB.Id, corrige: doB.Id));
        outroCiclo.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Item cujo tipo congela configuração é recusado: só o edital cria versão")]
    public async Task Tipo_QueCongelaConfiguracao_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        _congelado = _congelado with { CongelaConfiguracao = true };

        await DeveRecusar(Comando(preliminar.Id), AtoDoProcessoErrorCodes.TipoCongelaConfiguracao);
    }

    [Fact(DisplayName = "Item de tipo único por objeto com o ato anterior ainda pendente na Seleção é recusado")]
    public async Task Tipo_UnicoComVagaOcupadaNoRegistroLocal_ERecusado()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        _congelado = _congelado with { UnicoPorObjeto = true };
        _atos.ExisteAtoDoTipoForaDaCadeiaAsync(_processoId, Materia, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await DeveRecusar(Comando(preliminar.Id), "ProcessoSeletivo.ObjetoJaTemAtoVivoDoTipo");
    }

    [Fact(DisplayName = "Marcar o tipo como único no catálogo depois do preliminar não impede o definitivo: vale a marcação congelada na versão publicada")]
    public async Task Definitivo_DepoisDeMarcarOTipoComoUnicoNoCatalogo_NaoEImpedido()
    {
        (ProdutoDaFase preliminar, ProdutoDaFase definitivo) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar, definitivo));
        Publicado(Materia, preliminar.Id, registrado: true);
        Tipo(Materia, resultado: true, unico: true);
        _atos.ExisteAtoDoTipoForaDaCadeiaAsync(_processoId, Materia, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _vagas.ObjetoJaTemAtoDeOutraLinhagemAsync(
                EntidadesDeRegistroPeloDominio.ProcessoSeletivo, _processoId, Materia, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(definitivo.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>()
            .Which.AtributosDoTipo.UnicoPorObjeto.Should().BeFalse("o registro no Publicações leva a marcação congelada, e não a do catálogo de hoje");
    }

    [Fact(DisplayName = "Ligar congelaConfiguracao no catálogo depois da publicação não muda a classe da correção do resultado")]
    public async Task Correcao_DepoisDeLigarCongelaNoCatalogo_MantemAClasseCongelada()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        AtoDoProcesso original = Publicado(Materia, preliminar.Id, registrado: true);
        Tipo(Materia, resultado: true, congela: true);

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(Comando(preliminar.Id, corrige: original.Id));

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RegistrarAtoNormativoRequisicao>()
            .Which.AtributosDoTipo.CongelaConfiguracao.Should().BeFalse("a correção tem a classe do ato corrigido, que o edital congelou");
    }

    [Fact(DisplayName = "Desligar ehResultado no catálogo depois da publicação não dispensa a confirmação da política de divulgação")]
    public async Task Resultado_DepoisDeDesligarEhResultadoNoCatalogo_AindaExigeAPolitica()
    {
        (ProdutoDaFase preliminar, _) = Par();
        Cronograma(Fase("HOMOLOGACAO", 1, preliminar));
        Tipo(Materia, resultado: false);

        await DeveRecusar(Comando(preliminar.Id, politica: false), AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
    }

    private async Task DeveRecusar(PublicarProdutoDoEditalCommand comando, string codigoEsperado)
    {
        _atos.ClearReceivedCalls();
        _unitOfWork.ClearReceivedCalls();

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) = await Publicar(comando);

        resposta.IsFailure.Should().BeTrue();
        resposta.Error!.Code.Should().Be(codigoEsperado);
        mensagens.Should().BeEmpty("a recusa acontece antes de qualquer envio ao Publicações");
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoDoProcesso>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    private Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Publicar(PublicarProdutoDoEditalCommand comando) =>
        PublicarProdutoDoEditalCommandHandler.Handle(
            comando, _processos, _divulgados, _atos, _documentos, _codecs, _tipos, _vagas, _usuario, _unitOfWork,
            new RelogioFixo(Agora), CancellationToken.None);

    private PublicarProdutoDoEditalCommand Comando(Guid produtoId, bool politica = true, Guid? corrige = null) =>
        new(_processoId, produtoId, "5/2027", DataDoAto, "Diretor do CEPS", _principal.Id,
            PoliticaDeDivulgacaoConfirmada: politica, AtoCorrigidoId: corrige, MotivoDaCorrecao: corrige is null ? null : "Erro de digitação");

    /// <summary>O tipo no catálogo vivo, que a publicação só consulta para saber que ele existe e vigora.</summary>
    private void Tipo(string codigo, bool resultado, bool unico = false, bool congela = false) =>
        _tipos.ObterVigenteAsync(codigo, DataDoAto, Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(codigo, "Resultado da homologação", CongelaConfiguracao: congela, UnicoPorObjeto: unico, EfeitoIrreversivel: false, EhResultado: resultado));

    private void Cronograma(params FaseCronograma[] fases)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2027 — Medicina");
        _itensDoCronograma = IdentidadeDoItemDoEdital.Dos(fases, [.. processo.Etapas]);
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            _processoId, [1], Versao, "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", Agora);
        _processos.ObterVersaoVigenteAsync(_processoId, Agora, Arg.Any<CancellationToken>()).Returns(versao);
        _codecs.Reidratar(versao).Returns(_ => Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                fases, [.. processo.DocumentosExigidos], [], null, numeroDeOpcoesDeCurso: 1),
            DadosEdital.Criar("001/2027", Agora, Agora.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null,
            tiposDeAtoCongelados: new Dictionary<string, TipoDeAtoCongelado>(StringComparer.Ordinal) { [Materia] = _congelado })));
    }

    private static FaseCronograma Fase(string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseQueConclui(codigo, ordem, null, produtos);

    private static FaseCronograma FaseQueConclui(string codigo, int ordem, string? faseConcluinteCodigo, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false,
            inicio: Agora, fim: Agora.AddDays(10), produtos: produtos,
            faseConcluinteCodigo: faseConcluinteCodigo, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static (ProdutoDaFase Preliminar, ProdutoDaFase Definitivo) Par() =>
        (ProdutoDaFase.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaFase.Criar(Materia, PapelProdutoFase.Definitivo));

    /// <summary>Um ato já publicado do item, registrado na Seleção e conhecido como ato do processo.</summary>
    private AtoDoProcesso Publicado(string tipo, Guid produtoId, bool registrado)
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), _processoId, tipo, tipo, "1/2027", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
                Guid.CreateVersion7(), [], ItemDoEdital: _itensDoCronograma[produtoId]),
            new RelogioFixo(Agora)).Value!;
        typeof(AtoDoProcesso).GetProperty(nameof(AtoDoProcesso.CreatedAt))!.SetValue(ato, Agora.AddMinutes(-60 + _atosDoProcesso.Count));
        if (registrado)
        {
            ato.ConfirmarRegistro(new RelogioFixo(Agora));
        }

        _atosDoProcesso.Add(ato);
        _atos.ObterProcessoDoAtoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns(_processoId);
        _atos.ListarCadeiaDeCorrecaoAsync(ato.Id, Arg.Any<CancellationToken>()).Returns([ato.Id]);
        return ato;
    }

    private AtoDoProcesso RegistroDoAto(Guid atoId, string tipo) => AtoDoProcesso.Registrar(
        new NovoAtoDoProcesso(
            atoId, _processoId, tipo, tipo, "1/2027", DataDoAto, "Diretor do CEPS", "ceps", "psm-medicina-2027",
            Guid.CreateVersion7(), []),
        new RelogioFixo(Agora)).Value!;

    private static DocumentoAPublicar Documento()
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            ProcessoDosDocumentos, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, new RelogioFixo(Agora), TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), new RelogioFixo(Agora)).IsSuccess.Should().BeTrue();
        return documento;
    }
}
