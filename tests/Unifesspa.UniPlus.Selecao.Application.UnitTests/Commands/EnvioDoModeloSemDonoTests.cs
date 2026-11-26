namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class EnvioDoModeloSemDonoTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly Guid _id = Guid.CreateVersion7();
    private readonly IUnidadeDonaDoProcesso _donos = Substitute.For<IUnidadeDonaDoProcesso>();
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();
    private readonly IArquivoArmazenadoStorage _storage = Substitute.For<IArquivoArmazenadoStorage>();
    private readonly IUnitOfWork _unidadeDeTrabalho = Substitute.For<IUnitOfWork>();
    private readonly IRascunhoDeProcessoSeletivoRepository _rascunhos = Substitute.For<IRascunhoDeProcessoSeletivoRepository>();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();

    [Fact]
    public async Task Iniciar_SemDono_RecusaSemGerarUrl()
    {
        Result<IniciarEnvioDoModeloDeDocumentoDto> resultado = await IniciarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new(_id, "Declaração", "DOCX"), _donos, _modelos, _storage, _unidadeDeTrabalho, _relogio, default);

        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado().Code);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlUploadAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task Confirmar_SemDono_RecusaSemConsultarModelo()
    {
        Result<ModeloDeDocumentoDto> resultado = await ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new(_id, Guid.CreateVersion7()), new BloqueioCompartilhadoDoProcessoSeletivo(_rascunhos, _processos),
            _modelos, _storage, _unidadeDeTrabalho, _relogio, default);

        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado().Code);
        await _modelos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }

    [Fact]
    public async Task Acessar_SemDono_RecusaSemConsultarModelo()
    {
        Result<AcessoModeloDeDocumentoDto> resultado = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            new(_id, Guid.CreateVersion7()), _donos, _modelos, _storage, _relogio, default);

        resultado.Error!.Code.Should().Be(AcessoAoProcesso.NaoEncontrado().Code);
        await _modelos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
    }
    [Fact]
    public async Task Acessar_ModeloInexistente_RecusaSemGerarUrl()
    {
        _donos.DoProcessoAsync(_id, Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        Result<AcessoModeloDeDocumentoDto> resultado = await ObterAcessoModeloDeDocumentoQueryHandler.Handle(
            new(_id, Guid.CreateVersion7()), _donos, _modelos, _storage, _relogio, default);

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.NaoEncontrado);
        await _storage.DidNotReceiveWithAnyArgs().GerarUrlLeituraAsync(default!, default, default);
    }

    [Fact]
    public async Task Confirmar_ModeloInexistente_BloqueiaDonoAntesDaConsulta()
    {
        _rascunhos.BloquearCompartilhadoAsync(_id, Arg.Any<CancellationToken>()).Returns(true);
        Guid modeloId = Guid.CreateVersion7();
        Result<ModeloDeDocumentoDto> resultado = await ConfirmarEnvioDoModeloDeDocumentoCommandHandler.Handle(
            new(_id, modeloId), new BloqueioCompartilhadoDoProcessoSeletivo(_rascunhos, _processos),
            _modelos, _storage, _unidadeDeTrabalho, _relogio, default);

        resultado.Error!.Code.Should().Be(ModeloDeDocumentoErrorCodes.NaoEncontrado);
        Received.InOrder(() =>
        {
            _ = _rascunhos.BloquearCompartilhadoAsync(_id, Arg.Any<CancellationToken>());
            _ = _modelos.ObterPorIdAsync(modeloId, Arg.Any<CancellationToken>());
        });
    }
}
