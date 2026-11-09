namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Registro de que a classe da mudança de uma versão pendente se alterou e as aprovações do ciclo foram descartadas.
/// Só se acrescenta, nunca se altera: é evidência forense (ADR-0063).
/// </summary>
public sealed class RecalculoDaAprovacao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid VersaoId { get; private init; }

    public int CicloAnterior { get; private init; }

    public bool ExigiaAntes { get; private init; }

    public bool ExigeAgora { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    // Construtor de materialização do EF Core.
    private RecalculoDaAprovacao()
    {
    }

    internal static RecalculoDaAprovacao Registrar(Guid versaoId, int cicloAnterior, bool exigiaAntes, bool exigeAgora, DateTimeOffset instante) =>
        new()
        {
            VersaoId = versaoId,
            CicloAnterior = cicloAnterior,
            ExigiaAntes = exigiaAntes,
            ExigeAgora = exigeAgora,
            Instante = instante,
        };
}
