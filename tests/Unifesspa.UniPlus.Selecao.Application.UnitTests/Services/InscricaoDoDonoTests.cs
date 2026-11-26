namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class InscricaoDoDonoTests
{
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IUserContext _usuario = Substitute.For<IUserContext>();
    private readonly Guid _processoId = Guid.CreateVersion7();

    [Theory]
    [InlineData(null)]
    [InlineData("inválido")]
    public async Task ObterAtivaAsync_CpfAusenteOuInvalido_RecusaSemConsultar(string? cpf)
    {
        _usuario.Cpf.Returns(cpf);

        Result<Inscricao> resultado = await new InscricaoDoDono(_inscricoes, _usuario).ObterAtivaAsync(_processoId, "enviar a inscrição", CancellationToken.None);

        resultado.Error!.Code.Should().Be("Inscricao.CpfAusenteNoToken");
        await _inscricoes.DidNotReceiveWithAnyArgs().ObterAtivaPorProcessoECpfAsync(default, default!, default);
    }

    [Fact]
    public async Task ObterAtivaAsync_InscricaoAusente_RecusaComoNaoEncontrada()
    {
        _usuario.Cpf.Returns(PersonasDeTeste.FernandaDrumond.Valor);

        Result<Inscricao> resultado = await new InscricaoDoDono(_inscricoes, _usuario).ObterAtivaAsync(_processoId, "enviar a inscrição", CancellationToken.None);

        resultado.Error!.Code.Should().Be("Inscricao.NaoEncontrada");
    }

    [Theory]
    [InlineData("conta-original", true)]
    [InlineData("outra-conta", false)]
    public async Task ObterAtivaAsync_InscricaoEncontrada_ConfereConta(string conta, bool aceita)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        Inscricao inscricao = Inscricao.Iniciar(_processoId, cpf, "conta-original", "https://issuer", Guid.CreateVersion7()).Value!;
        _usuario.Cpf.Returns(cpf.Valor);
        _usuario.UserId.Returns(conta);
        _inscricoes.ObterAtivaPorProcessoECpfAsync(_processoId, cpf, Arg.Any<CancellationToken>()).Returns(inscricao);

        Result<Inscricao> resultado = await new InscricaoDoDono(_inscricoes, _usuario).ObterAtivaAsync(_processoId, "enviar a inscrição", CancellationToken.None);

        resultado.IsSuccess.Should().Be(aceita);
        if (!aceita)
        {
            resultado.Error!.Code.Should().Be("Inscricao.DonoDivergente");
        }
    }
}
