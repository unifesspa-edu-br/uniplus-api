namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A remoção do arquivo enviado para a inscrição: só vale no rascunho, e a inscrição que muda durante
/// a remoção não perde o arquivo.
/// </summary>
public sealed class RemoverArquivoDaInscricaoCommandHandlerTests
{
    private readonly CenarioDosDocumentosDaInscricaoDeTeste _cenario = new();

    [Fact(DisplayName = "Remover o arquivo pendente do rascunho apaga o registro e não agenda nada: o bucket de envio expira sozinho")]
    public async Task Remover_Rascunho_ApagaRegistroSemAgendarLimpeza()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);

        Result resultado = await _cenario.Remover(arquivo.Id);

        resultado.IsSuccess.Should().BeTrue();
        await _cenario.Inscricoes.Received(1).TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, true, Arg.Any<CancellationToken>());
        await _cenario.Documentos.Received(1).RemoverArquivoAsync(arquivo, Arg.Any<CancellationToken>());
        await _cenario.Armazenamento.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ObjectKey, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _cenario.Documentos.DidNotReceive().AgendarLimpezaAsync(arquivo.ChaveDefinitivaPrevista, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Depois do envio da inscrição o arquivo é retido: a remoção é recusada e nada é apagado")]
    public async Task Remover_InscricaoEnviada_Recusa()
    {
        Inscricao enviada = DescartarRascunhoDaInscricaoCommandHandlerTests.Enviada(_cenario.VersaoId);
        _cenario.Inscricoes.ObterAtivaPorProcessoECpfAsync(_cenario.ProcessoId, _cenario.Dono, Arg.Any<CancellationToken>()).Returns(enviada);
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(enviada);

        Result resultado = await _cenario.Remover(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.RemocaoSoNoRascunho);
        await _cenario.Documentos.DidNotReceive().RemoverArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
        await _cenario.Armazenamento.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Inscrição que virou envio ou cancelamento durante a remoção não perde o arquivo")]
    public async Task Remover_InscricaoAlterada_NaoApaga()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);
        _cenario.Inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, true, Arg.Any<CancellationToken>()).Returns(false);

        Result resultado = await _cenario.Remover(arquivo.Id);

        resultado.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        await _cenario.Documentos.DidNotReceive().RemoverArquivoAsync(Arg.Any<ArquivoDaApresentacao>(), Arg.Any<CancellationToken>());
        await _cenario.Armazenamento.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
