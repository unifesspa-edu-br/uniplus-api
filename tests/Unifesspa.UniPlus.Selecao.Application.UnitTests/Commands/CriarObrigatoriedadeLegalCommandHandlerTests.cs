namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A existência e a atividade do código de uma regra legal pertencem à Configuração;
/// a factory de domínio apenas preserva o código que o handler validou.
/// </summary>
public sealed class CriarObrigatoriedadeLegalCommandHandlerTests
{
    [Fact(DisplayName = "Handle aceita código de tipo ativo e persiste a obrigatoriedade")]
    public async Task Handle_TipoAtivo_Persiste()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_NOVO", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "PS_NOVO", "Processo novo", null));
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_NOVO"), repository, tipoReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await repository.Received(1).AdicionarAsync(
            Arg.Is<ObrigatoriedadeLegal>(regra => regra.TipoProcessoCodigo == "PS_NOVO"),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle recusa código inexistente ou desativado sem persistir regra")]
    public async Task Handle_TipoInexistenteOuInativo_Recusa()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        tipoReader.ObterAtivoPorCodigoAsync("PS_DESATIVADO", Arg.Any<CancellationToken>())
            .Returns((TipoProcessoView?)null);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra("PS_DESATIVADO"), repository, tipoReader, unitOfWork, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo");
        await repository.DidNotReceive().AdicionarAsync(Arg.Any<ObrigatoriedadeLegal>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle aceita o sentinela universal sem consultar os tipos ativos")]
    public async Task Handle_TipoUniversal_NaoConsultaTiposAtivos()
    {
        IObrigatoriedadeLegalRepository repository = Substitute.For<IObrigatoriedadeLegalRepository>();
        ITipoProcessoReader tipoReader = Substitute.For<ITipoProcessoReader>();
        ISelecaoUnitOfWork unitOfWork = Substitute.For<ISelecaoUnitOfWork>();
        repository.ExisteRegraCodigoAtivoAsync("REGRA_NOVA", null, Arg.Any<CancellationToken>()).Returns(false);

        Result<Guid> resultado = await CriarObrigatoriedadeLegalCommandHandler.Handle(
            NovaRegra(ObrigatoriedadeLegal.TipoProcessoUniversal), repository, tipoReader, unitOfWork, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        await tipoReader.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
    }

    private static CriarObrigatoriedadeLegalCommand NovaRegra(string tipoProcessoCodigo) => new(
        tipoProcessoCodigo,
        CategoriaObrigatoriedade.Etapa,
        "REGRA_NOVA",
        new EtapaObrigatoria("Prova objetiva"),
        "Descrição da regra.",
        "Lei de teste.",
        new DateOnly(2026, 1, 1),
        null,
        null,
        null);
}
