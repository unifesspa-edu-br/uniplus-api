namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Quem pratica um ato sobre a concessão, como o token o identifica, e quando: quem a concede ou quem a revoga. A
/// concessão grava os dois juntos na trilha.
/// </summary>
/// <param name="Agente">Emissor, <c>sub</c> e nome de exibição de quem age.</param>
/// <param name="Instante">O instante do ato, pelo relógio injetado.</param>
public sealed record AtoDoAgente(Identidade Agente, DateTimeOffset Instante)
{
    /// <summary>Quem age; nunca nulo, para que o ato não grave meia trilha.</summary>
    public Identidade Agente { get; } = Agente ?? throw new ArgumentNullException(nameof(Agente));
}
