namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Os motivos escolhidos pelo processo, lidos pela porta do processo nas transições que congelam versão.</summary>
internal static class MotivosDoProcessoDeTeste
{
    internal static IProcessoSeletivoRepository Com(IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ListarMotivosEscolhidosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(escolhidos);
        return repository;
    }
}
