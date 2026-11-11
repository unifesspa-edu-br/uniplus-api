namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class FasesIniciadasDeTeste
{
    /// <summary>O leitor de um processo em que nenhuma fase começou.</summary>
    internal static IFasesIniciadasDoProcesso Nenhuma() => Com(FasesIniciadas.Nenhuma);

    internal static IFasesIniciadasDoProcesso Com(FasesIniciadas iniciadas)
    {
        IFasesIniciadasDoProcesso leitor = Substitute.For<IFasesIniciadasDoProcesso>();
        leitor.ObterAsync(default, default, default).ReturnsForAnyArgs(iniciadas);
        return leitor;
    }
}
