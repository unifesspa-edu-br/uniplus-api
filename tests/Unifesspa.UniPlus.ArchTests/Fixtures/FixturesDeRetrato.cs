namespace Unifesspa.UniPlus.ArchTests.Fixtures.Canario;

/// <summary>Um tipo cujo nome leva "retrato": a regra tem de acusá-lo.</summary>
internal sealed class RetratoDoPerfilDeTeste
{
}

/// <summary>Um auxiliar de teste de nome limpo com um método que leva "retrato": a regra tem de acusar o método.</summary>
internal static class ApoioDeTeste
{
    internal static Task RebaixarRetratoAsync() => Task.CompletedTask;

    internal static Task RebaixarClassificacaoAsync() => Task.CompletedTask;
}
