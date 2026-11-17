namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O dono do documento a publicar na confirmação: o rascunho, enquanto o processo não nasceu, ou o processo publicado. A
/// bloqueio do dono vem antes de qualquer leitura do documento.
/// </summary>
public sealed class ConfirmarEnvioDoDocumentoAPublicarComDonoTests
{
    private readonly CenarioDoDocumentoAPublicar _cenario = new();

    [Fact(DisplayName = "Com o rascunho como dono, bloqueia o rascunho e confirma sem consultar o processo")]
    public async Task ComRascunho_BloqueiaORascunhoEConfirma()
    {
        DocumentoAPublicar documento = _cenario.ComRascunhoComoDono().PendenteComObjetoEnviado();

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.IsSuccess.Should().BeTrue();
        await _cenario.Rascunhos.Received(1).BloquearCompartilhadoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
        await _cenario.Processos.DidNotReceiveWithAnyArgs().ExisteAsync(default, default);
    }

    [Fact(DisplayName = "Sem rascunho, o processo publicado é o dono e a confirmação segue")]
    public async Task SemRascunho_ConfereOProcessoEConfirma()
    {
        DocumentoAPublicar documento = _cenario.ComProcessoPublicadoComoDono().PendenteComObjetoEnviado();

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.IsSuccess.Should().BeTrue();
        await _cenario.Processos.Received(1).ExisteAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem rascunho nem processo recusa 404 sem ler o documento nem confirmar")]
    public async Task SemDono_RecusaSemLerODocumento()
    {
        DocumentoAPublicar documento = _cenario.PendenteComObjetoEnviado();

        Result<DocumentoAPublicarDto> resultado = await _cenario.ConfirmarAsync(documento.Id);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _cenario.Storage.DidNotReceiveWithAnyArgs().SalvarConteudoSeladoAsync(default!, default!, default!, default);
    }

    [Fact(DisplayName = "O bloqueio do rascunho vem antes de ler o documento e de confirmá-lo: rascunho, depois documento")]
    public async Task OrdemDosBloqueios_RascunhoDepoisDocumento()
    {
        DocumentoAPublicar documento = _cenario.ComRascunhoComoDono().PendenteComObjetoEnviado();

        await _cenario.ConfirmarAsync(documento.Id);

        Received.InOrder(() =>
        {
            _ = _cenario.Rascunhos.BloquearCompartilhadoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
            _ = _cenario.Documentos.ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Documentos.TentarReivindicarConfirmacaoAsync(documento.Id, Arg.Any<CancellationToken>());
        });
    }
}
