namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ReativarFatoCandidatoCommandHandlerTests
{
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IConfiguracaoUnitOfWork _unitOfWork = Substitute.For<IConfiguracaoUnitOfWork>();

    private static FatoCandidato Declarado() =>
        FatoCandidato.CriarDoAdministrador(
            "OPTOU", "Optou", "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, null, null,
            "INSCRICAO", EscopoFato.Candidato, ClassificacaoDeProtecao.Pessoal,
            "Enquadramento na reserva de vagas.", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    [Fact(DisplayName = "Fato desativado é reativado e a alteração é gravada")]
    public async Task Handle_FatoDesativado_ReativaEGrava()
    {
        FatoCandidato fato = Declarado();
        fato.Desativar();
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        Result resultado = await ReativarFatoCandidatoCommandHandler.Handle(
            new ReativarFatoCandidatoCommand(fato.Id), _fatos, _unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        fato.Ativo.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Fato já ativo é recusado com JaAtivo e nada é gravado")]
    public async Task Handle_FatoJaAtivo_RecusaSemGravar()
    {
        FatoCandidato fato = Declarado();
        _fatos.ObterPorIdAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        Result resultado = await ReativarFatoCandidatoCommandHandler.Handle(
            new ReativarFatoCandidatoCommand(fato.Id), _fatos, _unitOfWork, CancellationToken.None);

        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.JaAtivo);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
