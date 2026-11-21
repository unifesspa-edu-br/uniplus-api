namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>Regra de ordem dos turnos de uma <see cref="Entities.OfertaCurso"/>.</summary>
public static class TurnosOferta
{
    /// <summary>
    /// Ordena os turnos na ordem canônica do dia — matutino, vespertino, noturno —, que é a ordem dos valores do enum.
    /// Fonte única da ordenação: a escrita normaliza antes de persistir e a leitura reordena, para que a promessa de ordem
    /// determinística do contrato valha mesmo sobre uma linha gravada fora do fluxo da aplicação.
    /// </summary>
    public static IReadOnlyList<TurnoOferta> OrdenarCanonicamente(IEnumerable<TurnoOferta> turnos)
    {
        ArgumentNullException.ThrowIfNull(turnos);
        return [.. turnos.Order()];
    }
}
