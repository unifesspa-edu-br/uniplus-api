namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System.Globalization;

using Microsoft.Extensions.Options;

public sealed class GatilhoDiarioOptionsValidator : IValidateOptions<GatilhoDiarioOptions>
{
    public ValidateOptionsResult Validate(string? name, GatilhoDiarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!TimeOnly.TryParseExact(
                options.Horario,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            failures.Add($"{nameof(options.Horario)} deve usar o formato HH:mm.");
        }

        if (string.IsNullOrWhiteSpace(options.FusoHorarioId))
        {
            failures.Add($"{nameof(options.FusoHorarioId)} é obrigatório.");
        }
        else
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(options.FusoHorarioId);
            }
            catch (TimeZoneNotFoundException)
            {
                failures.Add($"{nameof(options.FusoHorarioId)} não corresponde a um fuso horário instalado.");
            }
            catch (InvalidTimeZoneException)
            {
                failures.Add($"{nameof(options.FusoHorarioId)} corresponde a um fuso horário inválido.");
            }
        }

        bool configuracaoOperacionalInformada = !string.IsNullOrWhiteSpace(options.HorarioLimiteAlerta)
            || !string.IsNullOrWhiteSpace(options.FusoHorarioAlertaId)
            || !string.IsNullOrWhiteSpace(options.CanalAlerta)
            || options.RetryAposSegundos.Length > 0
            || options.PrazoReatribuicaoSegundos > 0;

        if (configuracaoOperacionalInformada && !TimeOnly.TryParseExact(
                options.HorarioLimiteAlerta,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            failures.Add($"{nameof(options.HorarioLimiteAlerta)} deve usar o formato HH:mm.");
        }

        if (configuracaoOperacionalInformada)
        {
            ValidarFuso(options.FusoHorarioAlertaId, nameof(options.FusoHorarioAlertaId), failures);

            if (string.IsNullOrWhiteSpace(options.CanalAlerta))
            {
                failures.Add($"{nameof(options.CanalAlerta)} é obrigatório.");
            }

            if (options.RetryAposSegundos.Length == 0 || options.RetryAposSegundos.Any(intervalo => intervalo <= 0))
            {
                failures.Add($"{nameof(options.RetryAposSegundos)} deve conter intervalos positivos em segundos.");
            }

            if (options.PrazoReatribuicaoSegundos <= 0)
            {
                failures.Add($"{nameof(options.PrazoReatribuicaoSegundos)} deve ser maior que zero.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidarFuso(string fusoHorarioId, string nome, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(fusoHorarioId))
        {
            failures.Add($"{nome} é obrigatório.");
            return;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(fusoHorarioId);
        }
        catch (TimeZoneNotFoundException)
        {
            failures.Add($"{nome} não corresponde a um fuso horário instalado.");
        }
        catch (InvalidTimeZoneException)
        {
            failures.Add($"{nome} corresponde a um fuso horário inválido.");
        }
    }
}
