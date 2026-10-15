namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

public static class CalculadoraDoProximoGatilhoDiario
{
    public static DateTimeOffset Calcular(
        DateTimeOffset agora,
        TimeOnly horario,
        TimeZoneInfo fusoHorario,
        DateOnly? depoisDe = null)
    {
        ArgumentNullException.ThrowIfNull(fusoHorario);

        DateOnly dataLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(agora, fusoHorario).DateTime);
        if (depoisDe is { } dataAnterior && dataLocal <= dataAnterior)
        {
            dataLocal = dataAnterior.AddDays(1);
        }

        while (true)
        {
            DateTime horarioLocal = DateTime.SpecifyKind(dataLocal.ToDateTime(horario), DateTimeKind.Unspecified);
            while (fusoHorario.IsInvalidTime(horarioLocal))
            {
                horarioLocal = horarioLocal.AddMinutes(1);
            }

            TimeSpan deslocamento = fusoHorario.IsAmbiguousTime(horarioLocal)
                ? fusoHorario.GetAmbiguousTimeOffsets(horarioLocal).Max()
                : fusoHorario.GetUtcOffset(horarioLocal);
            DateTimeOffset proximaExecucao = new(horarioLocal, deslocamento);

            bool respeitaDataAnterior = depoisDe is null
                || DateOnly.FromDateTime(horarioLocal) > depoisDe.Value;
            if (respeitaDataAnterior && proximaExecucao > agora)
            {
                return proximaExecucao;
            }

            dataLocal = dataLocal.AddDays(1);
        }
    }
}
