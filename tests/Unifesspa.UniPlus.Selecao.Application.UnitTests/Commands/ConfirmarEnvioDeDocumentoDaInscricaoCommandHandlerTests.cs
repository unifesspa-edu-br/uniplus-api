namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Testes.Compartilhado;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A confirmação do envio de documento da inscrição: o ETag, o arquivo pendente vencido,
/// a reivindicação da confirmação, a revisão da inscrição e o pedido de verificação do arquivo.
/// </summary>
public sealed class ConfirmarEnvioDeDocumentoDaInscricaoCommandHandlerTests
{
    private readonly CenarioDosDocumentosDaInscricaoDeTeste _cenario = new();

    [Fact(DisplayName = "Inscrição que mudou entre a leitura e a confirmação não recebe o arquivo nem o pedido de verificação")]
    public async Task Confirmar_InscricaoAlterada_NaoGravaNemPublica()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);
        _cenario.Inscricoes.TentarAvancarRevisaoAsync(inscricao.Id, inscricao.Revisao, false, Arg.Any<CancellationToken>()).Returns(false);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.InscricaoAlterada);
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Pendente);
        mensagens.Should().BeEmpty();
        await _cenario.Documentos.Received(1).DesfazerReivindicacaoAsync(arquivo.Id, Arg.Any<CancellationToken>());
        await _cenario.UnidadeDeTrabalho.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ETag em branco é recusado pelo domínio antes de reivindicar o arquivo")]
    public async Task Confirmar_ETagEmBranco_RecusaSemReivindicar()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id, etag: "  ");

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ETagObrigatorio);
        mensagens.Should().BeEmpty();
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().TentarReivindicarConfirmacaoAsync(default, default!, default, default, default);
    }

    [Fact(DisplayName = "Pendente vencido é recusado como expirado, sem disputar a revisão nem publicar pedido")]
    public async Task Confirmar_PendenteVencido_Recusa()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao, CriadoAntesDeConfirmar(TimeSpan.FromHours(3)));

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado);
        mensagens.Should().BeEmpty();
        await _cenario.Inscricoes.DidNotReceiveWithAnyArgs().TentarAvancarRevisaoAsync(default, default, default, default);
    }

    [Fact(DisplayName = "Pendente no instante em que o prazo de envio e a tolerância se esgotam é recusado como expirado")]
    public async Task Confirmar_NoVencimento_Recusa()
    {
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(_cenario.Rascunho(), CriadoAntesDeConfirmar(PrazoDeConfirmacao));

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ObjetoNaoEncontrado);
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "Pendente um microssegundo antes do vencimento ainda é confirmado")]
    public async Task Confirmar_ImediatamenteAntesDoVencimento_Confirma()
    {
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(
            _cenario.Rascunho(), CriadoAntesDeConfirmar(PrazoDeConfirmacao - TimeSpan.FromMicroseconds(1)));

        (Result<ArquivoDaApresentacaoDto> resposta, _) = await _cenario.Confirmar(arquivo.Id);

        resposta.IsSuccess.Should().BeTrue();
        arquivo.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.EmVerificacao);
    }

    [Fact(DisplayName = "Confirmação concorrente com a remoção do pendente vencido perde a reivindicação e é recusada, sem pedido")]
    public async Task Confirmar_ReivindicacaoPerdida_Recusa()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);
        _cenario.Documentos.TentarReivindicarConfirmacaoAsync(arquivo.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(false);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id);

        resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.StatusInvalidoParaConfirmacao);
        mensagens.Should().BeEmpty();
        await _cenario.Inscricoes.DidNotReceiveWithAnyArgs().TentarAvancarRevisaoAsync(default, default, default, default);
        await _cenario.UnidadeDeTrabalho.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Confirmar não lê o objeto: o arquivo fica em verificação e o pedido, com o ETag e o máximo da exigência, sai como mensagem da mesma transação")]
    public async Task Confirmar_Sucesso_PoeEmVerificacaoEPublicaOPedido()
    {
        Inscricao inscricao = _cenario.Rascunho();
        ArquivoDaApresentacao arquivo = _cenario.ArquivoPendente(inscricao);

        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(arquivo.Id);

        resposta.IsSuccess.Should().BeTrue();
        arquivo.ExpiraEm.Should().Be(DivulgacaoDeTeste.DentroDoPeriodo + PrazosDoArquivoEnviado.Envio, "o arquivo foi criado no mesmo instante em que o relógio da confirmação para");
        arquivo.ConfirmadoEm.Should().Be(DivulgacaoDeTeste.DentroDoPeriodo);
        ConferirRespostaDaConfirmacao(resposta.Value!);
        mensagens.Should().ContainSingle().Which.Should().Be(new PedidoDeVerificacaoDeArquivo(
            arquivo.Id, arquivo.ObjectKey, "\"etag-do-put\"", arquivo.ChaveDefinitivaPrevista, FormatoDoArquivo.Pdf, arquivo.TamanhoMaximoBytes));
        _cenario.Documentos.Received(1).Atualizar(arquivo);
        await _cenario.UnidadeDeTrabalho.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        await _cenario.Armazenamento.ReceivedWithAnyArgs(0).RemoverDefinitivoAsync(default!, default);
    }

    private static TimeSpan PrazoDeConfirmacao => PrazosDoArquivoEnviado.Envio + RemocaoDeArquivosPendentesVencidos.Tolerancia;

    private static RelogioFixo CriadoAntesDeConfirmar(TimeSpan antes) => new(DivulgacaoDeTeste.DentroDoPeriodo - antes);

    private static void ConferirRespostaDaConfirmacao(ArquivoDaApresentacaoDto resposta)
    {
        resposta.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.EmVerificacao);
        resposta.Mensagem.Should().Be("Arquivo em verificação. Isso costuma levar poucos segundos.");
        resposta.Rotulo.Should().Be("Arquivo 1");
        resposta.NomeParaDownload.Should().BeNull("o hash só existe depois da verificação");
    }
}
