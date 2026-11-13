namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O caso de uso do atendimento: as travas da revogação comum antes da leitura da providência, a conferência do e-mail
/// do beneficiário e o mínimo de dois gestores de acessos (UNI-REQ-0376 com UNI-REQ-0186).
/// </summary>
public sealed class AtendimentoDaProvidenciaTests
{
    private readonly CenarioDaProvidencia _cenario = new();
    private readonly ProvidenciaBuilder _construtor = new();

    [Fact(DisplayName = "O atendimento trava a concessão para alterar antes de ler a providência, revoga, grava e avisa os dois")]
    public async Task Atender_TravaRevogaGravaEAvisa()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AtenderAsync(providencia.Id, ProvidenciaBuilder.Leandro.Email);

        (resposta.Value!.Situacao, resposta.Value.Concessao.Ativa, resposta.Value.EncerramentoDaConcessao).Should().Be(("atendida", false, (EncerramentoDaConcessaoDto?)null));
        (mensagens.Count(), _construtor.Concessao.MotivoRevogacao).Should().Be((2, ProvidenciaBuilder.Motivo));
        Received.InOrder(() =>
        {
            _ = _cenario.Concessoes.ObterParaAlterarAsync(providencia.ConcessaoId, Arg.Any<CancellationToken>());
            _ = _cenario.Providencias.ObterPorIdAsync(providencia.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "O e-mail que não é o do beneficiário recusa o atendimento sem revogar, sem gravar e sem avisar")]
    public async Task Atender_EmailDivergente_NaoRevoga()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AtenderAsync(providencia.Id, "outra.pessoa@uniplus-test.local");

        (resposta.Errors.Single().Field, resposta.Error!.Code, mensagens.Count()).Should().Be(("confirmacaoEmail", ConcessaoErrorCodes.ConfirmacaoDivergente, 0));
        (_construtor.Concessao.RevogadaEm, providencia.Estado).Should().Be(((DateTimeOffset?)null, EstadoDaProvidencia.Aberta));
        await _cenario.Unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O atendimento que deixaria menos de dois gestores de acessos é recusado e nada é gravado")]
    public async Task Atender_MinimoDeGestores_Recusa()
    {
        Providencia providencia = _cenario.ComProvidencia(_construtor, static b => b.Aberta());
        _cenario.Concessoes.ObterGovernancaDoPerfilAsync(_construtor.Concessao.PerfilId, Arg.Any<CancellationToken>()).Returns(TipoDeGovernanca.GestorDeAcessos);
        _cenario.Concessoes.ContarGestoresAtivosAsync(default, default, default, default).ReturnsForAnyArgs(1);

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AtenderAsync(providencia.Id, ProvidenciaBuilder.Leandro.Email);

        (resposta.Error!.Code, mensagens.Count()).Should().Be((ConcessaoErrorCodes.MinimoDeGestores, 0));
        await _cenario.Concessoes.Received(1).TravarGovernancaParaEscritaAsync(Arg.Any<CancellationToken>());
        await _cenario.Unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A providência inexistente responde não encontrada, sem tomar as travas da revogação")]
    public async Task Atender_Inexistente_NaoEncontrada()
    {
        (Result<ProvidenciaDto> resposta, _) = await AtenderAsync(Guid.CreateVersion7(), ProvidenciaBuilder.Leandro.Email);

        resposta.Error!.Code.Should().Be(ProvidenciaErrorCodes.NaoEncontrada);
        await _cenario.Concessoes.DidNotReceiveWithAnyArgs().ObterParaAlterarAsync(default);
    }

    [Fact(DisplayName = "Sem emissor e sub no token, o atendimento é recusado antes de ler a providência")]
    public async Task Atender_AutorNaoIdentificado()
    {
        (Result<ProvidenciaDto> resposta, _) = await ProvidenciaCommandHandler.Handle(
            new AtenderProvidenciaCommand(Guid.CreateVersion7(), new DeclaracaoDoAtendimento(ProvidenciaBuilder.Motivo, null)),
            _cenario.Atendimento, Substitute.For<IUserContext>(), CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProvidenciaErrorCodes.AutorNaoIdentificado);
    }

    private Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> AtenderAsync(Guid providenciaId, string? confirmacaoEmail) =>
        ProvidenciaCommandHandler.Handle(
            new AtenderProvidenciaCommand(providenciaId, new DeclaracaoDoAtendimento(ProvidenciaBuilder.Motivo, confirmacaoEmail)),
            _cenario.Atendimento, Usuarios.De(ProvidenciaBuilder.Diogo), CancellationToken.None);
}
