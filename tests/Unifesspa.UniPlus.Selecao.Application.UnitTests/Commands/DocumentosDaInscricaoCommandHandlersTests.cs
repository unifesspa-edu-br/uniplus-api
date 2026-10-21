namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly IProtecaoDoConteudoSelado _protecao = Substitute.For<IProtecaoDoConteudoSelado>();
    private readonly Guid _versaoId;
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

        _storage.GerarUrlUploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.exemplo/envio");
        _inscricoes.TentarAvancarRevisaoAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private static FaseCronograma Fase(int ordem, string codigo, bool coletaInscricao) => FaseCronograma.Criar(
        ordem, Guid.CreateVersion7(), codigo, "CEPS", OrigemDataFase.Propria,
        agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar(codigo, PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false,
        bancasRequeridas: [], regraRecurso: null).Value!;

    private static DocumentoExigido Exigencia(string codigo, Guid faseId = default) => DocumentoExigido.Criar(
        faseId == Guid.Empty ? Guid.CreateVersion7() : faseId, Guid.CreateVersion7(), codigo, codigo, "PESSOAL",
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

    private Task<Result<ArquivoDaApresentacaoDto>> Confirmar(Guid arquivoId) =>
        ConfirmarEnvioDeDocumentoDaInscricaoCommandHandler.Handle(
            new ConfirmarEnvioDeDocumentoDaInscricaoCommand(ProcessoId, arquivoId),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _storage, _protecao, _unitOfWork, Usuario(),
            DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private Task<Result> Remover(Guid arquivoId) =>
        RemoverArquivoDaInscricaoCommandHandler.Handle(
            new RemoverArquivoDaInscricaoCommand(ProcessoId, arquivoId),
            _inscricoes, _processos, _certame, _codecs, DivulgacaoDeTeste.Fuso(), _documentos, _storage, _unitOfWork, Usuario(),
            DivulgacaoDeTeste.Relogio, CancellationToken.None);

    private ArquivoDaApresentacao ArquivoPendente(Inscricao inscricao)
    {
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), inscricao.Id, _laudo, "application/pdf", 2048, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        _documentos.ObterArquivoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(arquivo);
        _documentos.ObterApresentacaoAsync(arquivo.ApresentacaoId, Arg.Any<CancellationToken>())
            .Returns(ApresentacaoDeDocumento.Criar(inscricao.Id, _laudo.Id, null));
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
        ApresentacaoDeDocumento doLaudo = ApresentacaoDeDocumento.Criar(inscricao.Id, _laudo.Id, null);
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

        (await Confirmar(deOutra.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        (await Remover(deOutra.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        alheio.Status.Should().Be(StatusArquivoEnviado.Pendente);
        await _storage.DidNotReceive().RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição que mudou entre a leitura e a confirmação não recebe o arquivo")]
    public async Task Confirmar_InscricaoAlterada_NaoGrava()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);
        byte[] conteudo = [.. "%PDF-1.7"u8];
        _storage.ObterInfoAsync(arquivo.ObjectKey, Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(conteudo.Length, "application/pdf"));
        _storage.AbrirLeituraAsync(arquivo.ObjectKey, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(conteudo));
        _protecao.VerificarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>()).Returns(false);

        Result<ArquivoDaApresentacaoDto> resultado = await Confirmar(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        arquivo.Status.Should().Be(StatusArquivoEnviado.Pendente);
        await _storage.DidNotReceive().SalvarConteudoSeladoAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Confirmado o arquivo, o objeto do envio, em claro, é apagado depois de gravar")]
    public async Task Confirmar_Sucesso_ApagaOObjetoDoEnvio()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);
        byte[] conteudo = [.. "%PDF-1.7"u8];
        _storage.ObterInfoAsync(arquivo.ObjectKey, Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(conteudo.Length, "application/pdf"));
        _storage.AbrirLeituraAsync(arquivo.ObjectKey, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(conteudo));
        _protecao.VerificarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _protecao.SelarAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        _documentos.TentarReivindicarConfirmacaoAsync(arquivo.Id, Arg.Any<CancellationToken>()).Returns(true);

        Result<ArquivoDaApresentacaoDto> resultado = await Confirmar(arquivo.Id);

        resultado.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _unitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            _storage.RemoverAsync(arquivo.ObjectKey, Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Remover o arquivo do rascunho apaga o registro e as duas chaves do armazenamento")]
    public async Task Remover_Rascunho_ApagaRegistroEAsDuasChaves()
    {
        Inscricao inscricao = Rascunho();
        ArquivoDaApresentacao arquivo = ArquivoPendente(inscricao);

        Result resultado = await Remover(arquivo.Id);

        resultado.IsSuccess.Should().BeTrue();
        await _inscricoes.Received(1).TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, true, Arg.Any<CancellationToken>());
        await _documentos.Received(1).RemoverArquivoAsync(arquivo, Arg.Any<CancellationToken>());
        await _storage.Received(1).RemoverAsync(arquivo.ObjectKey, Arg.Any<CancellationToken>());
        await _storage.Received(1).RemoverAsync(arquivo.ChaveSelada, Arg.Any<CancellationToken>());
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
        await _storage.DidNotReceive().RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        await _storage.DidNotReceive().RemoverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
