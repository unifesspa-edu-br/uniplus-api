namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

/// <summary>
/// Marca que o alerta de ausência da sincronização já foi registrado para o dia.
/// </summary>
internal sealed class AlertaSincronizacaoDiaria
{
    private AlertaSincronizacaoDiaria()
    {
    }

    public DateOnly DataDeReferencia { get; private set; }

    public DateTimeOffset EmitidaEm { get; private set; }
}
