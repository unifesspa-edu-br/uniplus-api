namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Security.Cryptography;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O envio do modelo de documento: o início recusa o formato não editável antes de qualquer
/// storage; a confirmação valida o conteúdo contra o formato, sela com o content-type do formato e
/// devolve o hash; o acesso só assina o modelo confirmado.
/// </summary>
public sealed class EnvioDoModeloDeDocumentoHandlersTests
{
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();
    private readonly ISelecaoUnitOfWork _unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
    private readonly ModeloDeDocumento _modelo = ModeloDeDocumento.IniciarPendente(
        Guid.CreateVersion7(), "Autodeclaração", "DOCX", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;

    public EnvioDoModeloDeDocumentoHandlersTests()
    {
        _modelos.ObterPorIdAsync(_modelo.Id, Arg.Any<CancellationToken>()).Returns(_modelo);
        _modelos.TentarReivindicarConfirmacaoAsync(_modelo.Id, Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact(DisplayName = "O início com formato não editável é recusado sem gerar URL nem gravar")]
    public async Task Iniciar_FormatoNaoEditavel_RecusaSemStorage()
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ExisteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        Result<IniciarEnvioDoModeloDeDocumentoDto> resultado = await IniciarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new IniciarEnvioDoModeloDeDocumentoCommand(Guid.CreateVersion7(), "Autodeclaração", "PDF"),
            processos, _modelos, _storage, _unitOfWork, TimeProvider.System, CancellationToken.None);

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.FormatoNaoEditavel);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlUploadAsync(default!, default!, default, default);
        await _modelos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "A confirmação do DOCX sela com o content-type do formato e devolve o hash do conteúdo")]
    public async Task Confirmar_DocxValido_SelaEDevolveOHash()
    {
        byte[] conteudo = ArquivosDeModeloDeTeste.Docx();
        Enviado(conteudo, ArquivosDeModeloDeTeste.ContentTypeDocx);

        Result<ModeloDeDocumentoDto> resultado = await ConfirmarAsync();

        resultado.Value!.Should().BeEquivalentTo(new
        {
            Status = "Confirmado",
            Formato = "DOCX",
            NomeArquivo = "Autodeclaração.docx",
            HashSha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo)),
        });
        await _storage.Received(1).SalvarConteudoSeladoAsync(
            _modelo.ObjectKeyConfirmado!, Arg.Is<byte[]>(b => b.SequenceEqual(conteudo)), ArquivosDeModeloDeTeste.ContentTypeDocx, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O conteúdo fora do formato é recusado sem reivindicar a confirmação nem selar")]
    public async Task Confirmar_ConteudoForaDoFormato_RecusaSemSelar()
    {
        Enviado(ArquivosDeModeloDeTeste.PlanilhaComoDocx(), ArquivosDeModeloDeTeste.ContentTypeDocx);

        Result<ModeloDeDocumentoDto> resultado = await ConfirmarAsync();

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.ConteudoDivergeDoFormato);
        await _modelos.DidNotReceiveWithAnyArgs().TentarReivindicarConfirmacaoAsync(default, default);
        await _storage.DidNotReceiveWithAnyArgs().SalvarConteudoSeladoAsync(default!, default!, default!, default);
    }

    [Fact(DisplayName = "O arquivo acima do teto é recusado como tamanho excedido do modelo, sem baixar o conteúdo")]
    public async Task Confirmar_TamanhoExcedido_Recusa()
    {
        _storage.ObterInfoAsync(_modelo.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(new InfoObjetoArmazenado(ModeloDeDocumento.TamanhoMaximoBytes + 1, ArquivosDeModeloDeTeste.ContentTypeDocx));

        Result<ModeloDeDocumentoDto> resultado = await ConfirmarAsync();

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.TamanhoExcedido);
        await _storage.DidNotReceiveWithAnyArgs().AbrirLeituraAsync(default!, default, default);
    }

    [Fact(DisplayName = "O acesso ao modelo pendente é recusado; ao confirmado, assina a cópia selada")]
    public async Task Acesso_SoDoConfirmado()
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ExisteAsync(_modelo.ProcessoSeletivoId, Arg.Any<CancellationToken>()).Returns(true);
        _storage.GerarUrlLeituraAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns("https://storage.local/modelo");
        ObterAcessoModeloDeDocumentoQuery query = new(_modelo.ProcessoSeletivoId, _modelo.Id);

        Result<AcessoModeloDeDocumentoDto> pendente = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            query, processos, _modelos, _storage, TimeProvider.System, CancellationToken.None);
        _modelo.Confirmar(10, new string('a', 64), TimeProvider.System);
        Result<AcessoModeloDeDocumentoDto> confirmado = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            query, processos, _modelos, _storage, TimeProvider.System, CancellationToken.None);

        pendente.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.NaoConfirmado);
        confirmado.IsSuccess.Should().BeTrue();
        await _storage.Received(1).GerarUrlLeituraAsync(_modelo.ObjectKeyConfirmado!, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private void Enviado(byte[] conteudo, string contentType)
    {
        _storage.ObterInfoAsync(_modelo.ObjectKey, Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(conteudo.Length, contentType));
        _storage.AbrirLeituraAsync(_modelo.ObjectKey, Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(conteudo));
    }

    private Task<Result<ModeloDeDocumentoDto>> ConfirmarAsync() =>
        ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new ConfirmarEnvioDoModeloDeDocumentoCommand(_modelo.ProcessoSeletivoId, _modelo.Id),
            _modelos, _storage, _unitOfWork, TimeProvider.System, CancellationToken.None);
}
