namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O caso de uso da abertura: a trava antes da leitura, a gravação, os avisos e as recusas (UNI-REQ-0375).</summary>
public sealed class AberturaDaProvidenciaTests
{
    private readonly CenarioDaProvidencia _cenario = new();
    private readonly Concessao _concessao;

    public AberturaDaProvidenciaTests() => _concessao = _cenario.ComConcessao(ProvidenciaBuilder.ConcessaoDeLeandro());

    [Fact(DisplayName = "A abertura trava a concessão antes de ler a providência aberta, grava e responde com a providência aberta")]
    public async Task Abrir_TravaGravaEResponde()
    {
        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AbrirAsync(Valida());

        (resposta.Value!.Situacao, resposta.Value.Tipo, resposta.Value.ImpedidoDeResponder).Should().Be(("aberta", "retirada", true));
        mensagens.Should().HaveCount(3);
        Received.InOrder(() =>
        {
            _ = _cenario.Providencias.ObterConcessaoSobTravaCompartilhadaAsync(_concessao.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Providencias.ObterAbertaDaConcessaoAsync(_concessao.Id, Arg.Any<CancellationToken>());
            _ = _cenario.Providencias.AdicionarAsync(Arg.Any<Providencia>(), Arg.Any<CancellationToken>());
            _ = _cenario.Unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "A concessão inexistente responde não encontrada, sem gravar")]
    public async Task Abrir_ConcessaoInexistente_NaoEncontrada()
    {
        (Result<ProvidenciaDto> resposta, _) = await AbrirAsync(Valida(), Guid.CreateVersion7());

        resposta.Error!.Code.Should().Be(ConcessaoErrorCodes.NaoEncontrada);
        await _cenario.Unidade.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A solicitação inválida volta com as recusas acumuladas, sem gravar nem avisar")]
    public async Task Abrir_Invalida_NaoGrava()
    {
        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AbrirAsync(new SolicitacaoDaProvidencia(null, "curto"));

        resposta.Errors.Select(static e => e.Field).Should().Equal("tipo", "fundamento");
        mensagens.Should().BeEmpty();
        await _cenario.Providencias.DidNotReceive().AdicionarAsync(Arg.Any<Providencia>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A abertura concorrente que esbarra no índice único da aberta recebe a recusa de já haver aberta, sem avisos")]
    public async Task Abrir_IndiceUnicoNaGravacao_JaHaAberta()
    {
        _cenario.Unidade.SalvarAlteracoesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(ViolacaoDoIndiceDaAberta());

        (Result<ProvidenciaDto> resposta, IEnumerable<object> mensagens) = await AbrirAsync(Valida());

        (resposta.Error!.Code, mensagens.Count()).Should().Be((ProvidenciaErrorCodes.JaHaAberta, 0));
        _cenario.Unidade.Received(1).DescartarAlteracoesNaoSalvas();
    }

    [Fact(DisplayName = "Sem emissor e sub no token, a abertura é recusada antes de tocar a concessão")]
    public async Task Abrir_AutorNaoIdentificado_Recusa()
    {
        IUserContext semAutor = Substitute.For<IUserContext>();

        (Result<ProvidenciaDto> resposta, _) = await ProvidenciaCommandHandler.Handle(
            new SolicitarProvidenciaCommand(_concessao.Id, Valida()), _cenario.Abertura, semAutor, CancellationToken.None);

        resposta.Error!.Code.Should().Be(ProvidenciaErrorCodes.AutorNaoIdentificado);
        await _cenario.Providencias.DidNotReceiveWithAnyArgs().ObterConcessaoSobTravaCompartilhadaAsync(default);
    }

    private Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> AbrirAsync(SolicitacaoDaProvidencia solicitacao, Guid? concessaoId = null) =>
        ProvidenciaCommandHandler.Handle(
            new SolicitarProvidenciaCommand(concessaoId ?? _concessao.Id, solicitacao), _cenario.Abertura, Usuarios.De(ProvidenciaBuilder.Cecilia), CancellationToken.None);

    private static SolicitacaoDaProvidencia Valida() => new("retirada", ProvidenciaBuilder.Fundamento);

    private static Microsoft.EntityFrameworkCore.DbUpdateException ViolacaoDoIndiceDaAberta() =>
        new("violação", new ViolacaoDeIndiceUnico("23505", "ux_providencias_concessao_aberta"));
}

/// <summary>A exceção interna que o driver do banco levanta na violação de índice único, com o estado SQL e a restrição.</summary>
internal sealed class ViolacaoDeIndiceUnico(string sqlState, string constraintName) : Exception("violação de índice único")
{
    public string SqlState { get; } = sqlState;

    public string ConstraintName { get; } = constraintName;
}

/// <summary>O token de uma pessoa dos testes, com o nome e o e-mail que ele traz.</summary>
internal static class Usuarios
{
    public static IUserContext De(PessoaNoToken pessoa)
    {
        IUserContext usuario = Substitute.For<IUserContext>();
        usuario.Issuer.Returns(pessoa.Identidade.Emissor);
        usuario.UserId.Returns(pessoa.Identidade.Sub);
        usuario.NomeCompleto.Returns(pessoa.Nome);
        usuario.Email.Returns(pessoa.Email);
        usuario.EmailVerificado.Returns(pessoa.EmailVerificado);
        return usuario;
    }

    public static IUserContext De(Identidade pessoa) =>
        De(new PessoaNoToken(pessoa, pessoa.Nome, null, EmailVerificado: false));
}
