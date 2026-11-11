namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Os motivos escolhidos pelo processo, para os testes das transições que congelam versão.</summary>
internal static class MotivosDoProcessoDeTeste
{
    /// <summary>Processo que não escolheu motivo algum: sem isenção habilitada, a cobertura passa.</summary>
    internal static IMotivoDoProcessoRepository Nenhum() => Com([]);

    internal static IMotivoDoProcessoRepository Com(IReadOnlyList<MotivoEscolhidoComOrigem> escolhidos)
    {
        IMotivoDoProcessoRepository repository = Substitute.For<IMotivoDoProcessoRepository>();
        repository.ListarComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(escolhidos);
        repository.ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([.. escolhidos.Select(static e => e.Escolhido)]);
        return repository;
    }
}
