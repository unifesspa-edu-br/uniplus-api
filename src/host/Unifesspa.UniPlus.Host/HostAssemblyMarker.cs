namespace Unifesspa.UniPlus.Host;

/// <summary>
/// Marcador de assembly do composition root do monólito modular.
/// Usado por test factories e fitness tests para localizar o host.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Marcador referenciado por testes/fitness fora deste assembly.")]
public sealed class HostAssemblyMarker
{
    private HostAssemblyMarker()
    {
    }
}
