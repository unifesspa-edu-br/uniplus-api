namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>
/// Entidade CRUD trivial do módulo de prova de coabitação — escrita via EF Core,
/// no mesmo host/processo que os agregados event-sourced do Marten.
/// </summary>
public sealed class RegistroCrud
{
    public Guid Id { get; set; }

    public string Descricao { get; set; } = string.Empty;
}
