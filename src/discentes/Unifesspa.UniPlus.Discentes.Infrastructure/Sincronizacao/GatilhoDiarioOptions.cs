namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

public sealed class GatilhoDiarioOptions
{
    public const string SectionName = "Discentes:GatilhoDiario";

    public string Horario { get; set; } = string.Empty;

    public string FusoHorarioId { get; set; } = string.Empty;

    public string HorarioLimiteAlerta { get; set; } = string.Empty;

    public string FusoHorarioAlertaId { get; set; } = string.Empty;

    public string CanalAlerta { get; set; } = string.Empty;

    public int[] RetryAposSegundos { get; set; } = [];

    public int PrazoReatribuicaoSegundos { get; set; }
}
