namespace Unifesspa.UniPlus.ArchTests.Fixtures.DependenciasDeIngresso;

public sealed class UsaWolverine(Wolverine.IMessageBus barramento)
{
    public Wolverine.IMessageBus Barramento { get; } = barramento;
}
