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
/// do beneficiário, o mínimo de dois gestores de acessos e a regra do último Representante do controlador (UNI-REQ-0376
/// com UNI-REQ-0186 e UNI-REQ-0394).
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

    [Fact(DisplayName = "O atendimento sobre a única designação vigente de Representante é recusado sob a trava do papel, sem desfecho gravado")]
    public async Task Atender_UltimoRepresentante_Recusa()
    {
        Concessao designacao = DesignacaoDoRepresentante.De(ProvidenciaBuilder.Leandro.Identidade, ProvidenciaBuilder.Leandro.Email, ProvidenciaBuilder.Agora);
        Providencia providencia = _cenario.ComProvidencia(_construtor.SobreA(designacao), static b => b.Aberta());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AtenderAsync(providencia.Id, ProvidenciaBuilder.Leandro.Email);

        (resposta.Error!.Code, mensagens.Count()).Should().Be((ConcessaoErrorCodes.UltimoRepresentante, 0));
        Received.InOrder(() =>
        {
            _ = _cenario.Papel.TravarAsync(Arg.Any<CancellationToken>());
            _ = _cenario.Concessoes.ObterParaAlterarAsync(designacao.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Papel.HaOutroVigenteAsync(ProvidenciaBuilder.Leandro.Identidade, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        });
        _cenario.Unidade.Received().DescartarAlteracoesNaoSalvas();
        await _cenario.Unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com outra pessoa vigente no papel, o atendimento revoga a designação de Representante e grava")]
    public async Task Atender_RepresentanteComOutroVigente_Revoga()
    {
        Concessao designacao = DesignacaoDoRepresentante.De(ProvidenciaBuilder.Leandro.Identidade, ProvidenciaBuilder.Leandro.Email, ProvidenciaBuilder.Agora);
        Providencia providencia = _cenario.ComProvidencia(_construtor.SobreA(designacao), static b => b.Aberta());
        _cenario.Papel.HaOutroVigenteAsync(default, default, default).ReturnsForAnyArgs(true);

        (Result<ProvidenciaDto> resposta, _) = await AtenderAsync(providencia.Id, ProvidenciaBuilder.Leandro.Email);

        (resposta.Value!.Situacao, designacao.RevogadaEm is not null).Should().Be(("atendida", true));
        await _cenario.Unidade.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
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
