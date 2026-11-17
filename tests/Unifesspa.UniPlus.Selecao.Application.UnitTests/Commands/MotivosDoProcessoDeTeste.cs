namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A escolha atual do processo, lida pela porta dos motivos nas transições que congelam versão.</summary>
internal static class MotivosDoProcessoDeTeste
{
    internal static IMotivoDoProcessoRepository Com(IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos)
    {
        IMotivoDoProcessoRepository repository = Substitute.For<IMotivoDoProcessoRepository>();
        repository.ListarEscolhaAtualComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(escolhidos);
        return repository;
    }
}
