namespace Unifesspa.UniPlus.Discentes.API;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Wolverine;

/// <summary>
/// Declara quais portas do módulo Discentes são obtidas do contêiner em tempo de execução,
/// em vez de construídas pelo código gerado (ADR-0098).
/// </summary>
/// <remarks>
/// O registro de execuções é resolvido por fábrica, opaca ao gerador, porque abre escopo
/// próprio para cada marco que grava. Sem esta declaração a montagem do pipeline falha na
/// subida — que é onde se quer descobrir isso.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class DiscentesCodegenRegistration
{
    public static void ConfigurarCodegenWolverine(WolverineOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);

        opts.CodeGeneration.AlwaysUseServiceLocationFor<IRegistroDeExecucoes>();
    }
}
