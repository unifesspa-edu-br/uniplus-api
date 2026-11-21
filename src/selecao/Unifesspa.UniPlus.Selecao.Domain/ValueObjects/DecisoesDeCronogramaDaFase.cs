namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que o edital decide sobre quando a fase ocorre e quem responde por ela. É entrada de fábrica de vida curta, só dados: o período chega no offset que o
/// cliente escolheu, e quem a fixa em UTC é a <see cref="Entities.FaseCronograma"/>.
/// </summary>
public sealed record DecisoesDeCronogramaDaFase(
    int Ordem,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    UnidadeResponsavelSnapshot? UnidadeResponsavel)
{
    /// <summary>A fase acontece no mesmo momento da fase que a antecede na lista; sem a marca, acontece depois dela.</summary>
    public bool NoMesmoMomentoDaAnterior { get; init; }
}
