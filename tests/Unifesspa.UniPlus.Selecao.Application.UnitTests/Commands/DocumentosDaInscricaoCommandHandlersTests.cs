namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Os três passos do envio de documento da inscrição: o dono do token, a exigência na árvore congelada da
/// versão da inscrição, a ocorrência, a apresentação reaproveitada só por id explícito e a disputa da
/// revisão da inscrição com o envio e o cancelamento.
/// </summary>
public sealed class DocumentosDaInscricaoCommandHandlersTests
{
    private const string Grupo = "DEPENDENTES";

    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Cpf Dono = PersonasDeTeste.KevinCarvalho;

    private readonly DocumentoExigido _laudo;
    private readonly DocumentoExigido _certidao;
    private readonly DocumentoExigido _deOutraFase;
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();
    private readonly IDocumentoDaInscricaoRepository _documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
    private readonly IArquivoDoCandidatoStorage _storage = Substitute.For<IArquivoDoCandidatoStorage>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly Guid _versaoId;
    private readonly VersaoConfiguracao _versao;
    private readonly GrafoConfiguracao _grafo;
    private readonly ICertameDivulgadoRepository _certame = DivulgacaoDeTeste.Certame(ProcessoId);

    public DocumentosDaInscricaoCommandHandlersTests()
    {
        (VersaoConfiguracao versao, GrafoConfiguracao conforme) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        FaseCronograma faseDaInscricao = Fase(1, "INSCRICAO", coletaInscricao: true);
        FaseCronograma outraFase = Fase(2, "RESULTADO_FINAL", coletaInscricao: false);
        _laudo = Exigencia("LAUDO", faseDaInscricao.Id);
        _certidao = Exigencia("CERTIDAO", faseDaInscricao.Id);
        _deOutraFase = Exigencia("RESULTADO", outraFase.Id);

        // O laudo é uma folha simples; a certidão vale por ocorrência do grupo de dependentes.
        NoExigencia folhaLaudo = NoExigencia.CriarFolha(_laudo, 0).Value!;
        NoExigencia folhaCertidao = NoExigencia.CriarFolha(_certidao, 1, repetePorEntidade: Grupo).Value!;
        NoExigencia folhaOutraFase = NoExigencia.CriarFolha(_deOutraFase, 2).Value!;
        GrafoConfiguracao grafo = new(
            conforme.Etapas, conforme.OfertaAtendimento, conforme.DistribuicaoVagas, conforme.BonusRegional, conforme.CriteriosDesempate,
            conforme.Classificacao, [faseDaInscricao, outraFase], [_laudo, _certidao, _deOutraFase], [folhaLaudo, folhaCertidao, folhaOutraFase], null,
            fatosColetados: conforme.FatosColetados, formularios: conforme.Formularios, gruposColetados: conforme.GruposColetados,
            numeroDeOpcoesDeCurso: 1);
        DivulgacaoDeTeste.ConfigurarVersao(_processos, _codecs, versao, grafo);
        _versaoId = versao.Id;
        _versao = versao;
        _grafo = grafo;

        _storage.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.exemplo/envio");
        _inscricoes.TentarAvancarRevisaoAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
        _documentos.TentarReivindicarConfirmacaoAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.NoEdital,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static DocumentoExigido Exigencia(string codigo, Guid faseId = default) => DocumentoExigido.Criar(
        faseId == Guid.Empty ? Guid.CreateVersion7() : faseId, Guid.CreateVersion7(), codigo, codigo, "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
        Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
        [], [], null, FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;

    private Inscricao Rascunho(params string[] dependentes)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal)
        {
            [Grupo] = [.. dependentes.Select(id => new OcorrenciaDeGrupoInscricao(id, new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal)))],
        };
        Inscricao inscricao = Inscricao.Reidratar(
            Guid.CreateVersion7(), ProcessoId, Dono, "sub-1", "https://issuer", SituacaoInscricao.Rascunho, _versaoId, null, null, null,
            null, null, null, new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal),
            new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal), grupos, [],
            new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal), revisao: 3, DateTimeOffset.UnixEpoch, null);
        _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(inscricao);
        return inscricao;
    }

    private static IUserContext Usuario(string sub = "sub-1")
    {
        IUserContext contexto = Substitute.For<IUserContext>();
        contexto.Cpf.Returns(Dono.Valor);
        contexto.UserId.Returns(sub);
        return contexto;
    }

    private Task<Result<IniciarEnvioDeDocumentoDaInscricaoDto>> Iniciar(
        DocumentoExigido exigencia, string? ocorrenciaId = null, Guid? apresentacaoId = null, DateTimeOffset? agora = null) =>
        IniciarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(
            new IniciarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, exigencia.Id, ocorrenciaId, apresentacaoId, "application/pdf", 2048),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _storage, _unitOfWork, Usuario(),
            new RelogioFixo(agora ?? DivulgacaoDeTeste.DentroDoPeriodo), CancellationToken.None);

    private Task<(Result<ArquivoDaApresentacaoDto> Resposta, IEnumerable<object> Mensagens)> Confirmar(Guid arquivoId) =>
        ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(
            new ConfirmarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, arquivoId, "\"etag-do-put\""),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _unitOfWork,
            Options.Create(new ReconciliacaoDaVerificacaoOptions()), Usuario(), DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private Task<Result> Remover(Guid arquivoId) =>
        RemoverArquivoDaInscricaoCommandHandler.Handle(
            new RemoverArquivoDaInscricaoCommand(ProcessoId, arquivoId),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _unitOfWork,
            Usuario(), DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private ArquivoDaApresentacao ArquivoPendente(Inscricao inscricao, TimeProvider? relogio = null)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(inscricao.Id, _laudo, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, inscricao.Id, _laudo, "application/pdf", 2048, relogio ?? TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        _documentos.ObterApresentacaoAsync(arquivo.ApresentacaoId, Arg.Any<CancellationToken>()).Returns(apresentacao);
        _documentos.ListarApresentacoesDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns([apresentacao]);
        _documentos.ListarArquivosDaInscricaoAsync(inscricao.Id, Arg.Any<CancellationToken>()).Returns([arquivo]);
        return arquivo;
    }

    [Fact(DisplayName = "Iniciar o envio de uma exigência da versão da inscrição cria a apresentação e o arquivo pendente e devolve a URL")]
    public async Task Iniciar_ExigenciaDaVersao_CriaApresentacaoEArquivo()
    {
        Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await Iniciar(_laudo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ContentTypeExigido.Should().Be("application/pdf");
        await _documentos.Received(1).AdicionarApresentacaoAsync(Arg.Any<ApresentacaoDeDocumento>(), Arg.Any<CancellationToken>());
        await _documentos.Received(1).AdicionarArquivoAsync(
            Arg.Is<ArquivoDaApresentacao>(a => a.Id == resultado.Value.ArquivoId && a.ObjectKey.Contains(a.Id.ToString("D"), StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O iniciar avança a revisão da inscrição, e quem perde a disputa não cria arquivo")]
    public async Task Iniciar_DisputaDaRevisao_PerdidaNaoCriaArquivo()
    {
        Inscricao inscricao = Rascunho();
        (await Iniciar(_laudo)).IsSuccess.Should().BeTrue();
        await _inscricoes.Received(1).TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>());

        _documentos.ClearReceivedCalls();
        _inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>()).Returns(false);

        (await Iniciar(_laudo)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        await _documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho que adota a versão divulgada tem a adoção gravada junto do envio pendente")]
    public async Task Iniciar_AdocaoDeVersao_EGravada()
    {
        Inscricao inscricao = Rascunho();
        VersaoConfiguracao divulgada = DivulgacaoDeTeste.VersaoConforme(ProcessoId).Versao;
        DivulgacaoDeTeste.ConfigurarFixadaEDivulgada(
            _processos, _codecs, _grafo, fixada: (_versao, DivulgacaoDeTeste.FimDoPeriodo), divulgada: (divulgada, DivulgacaoDeTeste.FimDoPeriodo));

        (await Iniciar(_laudo)).IsSuccess.Should().BeTrue();

        inscricao.VersaoFormularioId.Should().Be(divulgada.Id);
        await _inscricoes.Received(1).AtualizarAsync(inscricao, Arg.Any<CancellationToken>());
        await _inscricoes.DidNotReceive().TentarAvancarRevisaoAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Exigência que não está na árvore congelada da versão da inscrição não recebe arquivo")]
    public async Task Iniciar_ExigenciaForaDaVersao_Recusa()
    {
        Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await Iniciar(Exigencia("OUTRA"));

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada);
        await _documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Exigência de outra fase que não a da inscrição não recebe arquivo")]
    public async Task Iniciar_ExigenciaDeOutraFase_Recusa()
    {
        Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await Iniciar(_deOutraFase);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ExigenciaNaoEncontrada);
        await _documentos.DidNotReceive().AdicionarArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "A exigência por ocorrência pede uma ocorrência que a inscrição tem; a que não se repete não admite ocorrência")]
    [InlineData("LAUDO", "dep-1", false)]
    [InlineData("CERTIDAO", null, false)]
    [InlineData("CERTIDAO", "dep-9", false)]
    [InlineData("CERTIDAO", "dep-1", true)]
    public async Task Iniciar_Ocorrencia_ConfereComAExigencia(string exigencia, string? ocorrencia, bool aceita)
    {
        Rascunho("dep-1", "dep-2");

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await Iniciar(exigencia == "LAUDO" ? _laudo : _certidao, ocorrencia);

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.OcorrenciaInvalida);
        }
    }

    [Fact(DisplayName = "A apresentação só é reaproveitada com o id explícito e da mesma exigência; sem o id, abre-se outra")]
    public async Task Iniciar_Apresentacao_SoReaproveitaPorIdExplicito()
    {
        Inscricao inscricao = Rascunho("dep-1");
        ApresentacaoDeDocumento doLaudo = ApresentacaoDeDocumento.Criar(inscricao.Id, _laudo, null);
        _documentos.ObterApresentacaoAsync(doLaudo.Id, Arg.Any<CancellationToken>()).Returns(doLaudo);

        (await Iniciar(_certidao, "dep-1", doLaudo.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ApresentacaoNaoEncontrada);

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> verso = await Iniciar(_laudo, apresentacaoId: doLaudo.Id);
        verso.Value!.ApresentacaoId.Should().Be(doLaudo.Id);
        await _documentos.DidNotReceive().AdicionarApresentacaoAsync(Arg.Any<ApresentacaoDeDocumento>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com as inscrições encerradas o envio é recusado")]
    public async Task Iniciar_ForaDoPeriodo_Recusa()
    {
        Rascunho();

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await Iniciar(_laudo, agora: DivulgacaoDeTeste.DentroDoPeriodo.AddMonths(1));

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ForaDoPeriodo);
    }

    [Fact(DisplayName = "Quem não tem inscrição no processo recebe a recusa neutra de inscrição não encontrada")]
    public async Task Iniciar_SemInscricao_Recusa()
    {
        _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        (await Iniciar(_laudo)).Error!.Code.Should().Be(InscricaoErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "Arquivo de outra inscrição responde como arquivo inexistente, na confirmação e na remoção")]
    public async Task ArquivoDeOutraInscricao_NaoEncontrado()
    {
        Rascunho();
        ArquivoDaApresentacao alheio = ArquivoPendente(Rascunho());
        ArquivoDaApresentacao deOutra = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), _laudo, "application/pdf", 2048, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        _documentos.ObterArquivoAsync(deOutra.Id, Arg.Any<CancellationToken>()).Returns(deOutra);

        (await Confirmar(deOutra.Id)).Resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        (await Remover(deOutra.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        alheio.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Pendente);
        await _storage.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição que mudou entre a leitura e a confirmação não recebe o arquivo nem o pedido de verificação")]
    public async Task Confirmar_InscricaoAlterada_NaoGravaNemPublica()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);
        _inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>()).Returns(false);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Pendente);
        mensagens.Should().BeEmpty();
        await _documentos.Received(1).DesfazerReivindicacaoAsync(arquivo.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ETag em branco é recusado pelo domínio antes de reivindicar o arquivo")]
    public async Task Confirmar_ETagEmBranco_RecusaSemReivindicar()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(
            new ConfirmarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, arquivo.Id, "  "),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _unitOfWork,
            Options.Create(new ReconciliacaoDaVerificacaoOptions()), Usuario(), DivulgacaoDeTeste.Relogio, CancellationToken.None);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ETagObrigatorio);
        mensagens.Should().BeEmpty();
        await _documentos.DidNotReceiveWithAnyArgs().TentarReivindicarConfirmacaoAsync(default, default!, default, default, default);
    }

    [Fact(DisplayName = "Pendente vencido é recusado como expirado, sem disputar a revisão nem publicar pedido")]
    public async Task Confirmar_PendenteVencido_Recusa()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao, new RelogioFixo(DivulgacaoDeTeste.Relogio.GetUtcNow() - TimeSpan.FromHours(3)));

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado);
        mensagens.Should().BeEmpty();
        await _inscricoes.DidNotReceiveWithAnyArgs().TentarAvancarRevisaoAsync(default, default, default, default);
    }

    [Fact(DisplayName = "Confirmação concorrente com a remoção do pendente vencido perde a reivindicação e é recusada, sem pedido")]
    public async Task Confirmar_ReivindicacaoPerdida_Recusa()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);
        _documentos.TentarReivindicarConfirmacaoAsync(arquivo.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao);
        mensagens.Should().BeEmpty();
        await _inscricoes.DidNotReceiveWithAnyArgs().TentarAvancarRevisaoAsync(default, default, default, default);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Confirmar não lê o objeto: o arquivo fica em verificação e o pedido, com o ETag e o máximo da exigência, sai como mensagem da mesma transação")]
    public async Task Confirmar_Sucesso_PoeEmVerificacaoEPublicaOPedido()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await Confirmar(arquivo.Id);

        resposta.IsSuccess.Should().BeTrue();
        resposta.Value!.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.EmVerificacao);
        resposta.Value.Mensagem.Should().Be("Arquivo em verificação. Isso costuma levar poucos segundos.");
        resposta.Value.Rotulo.Should().Be("Arquivo 1");
        resposta.Value.NomeParaDownload.Should().BeNull("o hash só existe depois da verificação");
        mensagens.Should().ContainSingle().Which.Should().Be(new PedidoDeVerificacaoDeArquivo(
            arquivo.Id, arquivo.ObjectKey, "\"etag-do-put\"", arquivo.ChaveDefinitivaPrevista, FormatoDoArquivo.Pdf, arquivo.TamanhoMaximoBytes));
        _documentos.Received(1).Atualizar(arquivo);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _storage.ReceivedWithAnyArgs(0).RemoverDefinitivoAsync(default!, default);
    }

    [Fact(DisplayName = "Remover o arquivo pendente do rascunho apaga o registro e não agenda nada: o bucket de envio expira sozinho")]
    public async Task Remover_Rascunho_ApagaRegistroEAgendaAsDuasChaves()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);

        Result resultado = await Remover(arquivo.Id);

        resultado.IsSuccess.Should().BeTrue();
        await _inscricoes.Received(1).TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, true, Arg.Any<CancellationToken>());
        await _documentos.Received(1).RemoverArquivoAsync(arquivo, Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ObjectKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Depois do envio da inscrição o arquivo é retido: a remoção é recusada e nada é apagado")]
    public async Task Remover_InscricaoEnviada_Recusa()
    {
        Inscricao enviada = DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada(_versaoId);
        _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, Dono, Arg.Any<CancellationToken>()).Returns(enviada);
        ArquivoDaApresentacao arquivo = ArquivoPendente(enviada);

        Result resultado = await Remover(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.RemocaoSoNoRascunho);
        await _documentos.DidNotReceive().RemoverArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição que virou envio ou cancelamento durante a remoção não perde o arquivo")]
    public async Task Remover_InscricaoAlterada_NaoApaga()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);
        _inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, true, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await Remover(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        await _documentos.DidNotReceive().RemoverArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
