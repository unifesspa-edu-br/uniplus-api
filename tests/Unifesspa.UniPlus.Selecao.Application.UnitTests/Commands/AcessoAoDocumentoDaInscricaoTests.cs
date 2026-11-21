namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// O acesso do dono do token aos documentos da inscrição: o CPF do token, quem não tem inscrição no
/// processo e o arquivo de outra inscrição.
/// </summary>
public sealed class AcessoAoDocumentoDaInscricaoTests
{
    private readonly CenarioDosDocumentosDaInscricaoDeTeste _cenario = new();

    [Theory(DisplayName = "Sem CPF válido no token a confirmação é recusada antes de ler a inscrição")]
    [InlineData(null)]
    [InlineData("inválido")]
    public async Task Confirmar_CpfAusenteOuInvalido_RecusaSemLerInscricao(string? cpf)
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.Cpf.Returns(cpf);
        (Result<ArquivoDaApresentacaoDto> resposta, IEnumerable<object> mensagens) = await _cenario.Confirmar(Guid.CreateVersion7(), usuario);
        resposta.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        mensagens.Should().BeEmpty();
        await _cenario.Inscricoes.DidNotReceiveWithAnyArgs().ObterAtivaPorProcessoECpfAsync(default, default!, default);
        await _cenario.UnidadeDeTrabalho.DidNotReceiveWithAnyArgs().SalvarAlteracoesAsync(default);
    }

    [Fact(DisplayName = "Quem não tem inscrição no processo recebe a recusa neutra de inscrição não encontrada")]
    public async Task Iniciar_SemInscricao_Recusa()
    {
        _cenario.Inscricoes.ObterAtivaPorProcessoECpfAsync(_cenario.ProcessoId, _cenario.Dono, Arg.Any<CancellationToken>()).Returns((Inscricao?)null);

        (await _cenario.Iniciar(_cenario.Laudo)).Error!.Code.Should().Be(InscricaoErrorCodes.NaoEncontrada);
    }

    [Fact(DisplayName = "Arquivo de outra inscrição responde como arquivo inexistente, na confirmação e na remoção")]
    public async Task ArquivoDeOutraInscricao_NaoEncontrado()
    {
        ArquivoDaApresentacao deOutra = ArquivoDaApresentacao.IniciarPendente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), _cenario.Laudo, "application/pdf", 2048, DivulgacaoDeTeste.Relogio, PrazosDoArquivoEnviado.Envio).Value!;
        _cenario.ArquivoPendente(_cenario.Rascunho());
        _cenario.Documentos.ObterArquivoAsync(deOutra.Id, Arg.Any<CancellationToken>()).Returns(deOutra);

        (await _cenario.Confirmar(deOutra.Id)).Resposta.Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        (await _cenario.Remover(deOutra.Id)).Error!.Code.Should().Be(DocumentoDaInscricaoErrorCodes.ArquivoNaoEncontrado);
        deOutra.Situacao.Should().Be(SituacaoDoArquivoDoCandidato.Pendente);
        _cenario.Documentos.DidNotReceive().Atualizar(deOutra);
        await _cenario.Documentos.DidNotReceive().RemoverArquivoAsync(deOutra, Arg.Any<CancellationToken>());
        await _cenario.Armazenamento.DidNotReceive().RemoverDefinitivoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
