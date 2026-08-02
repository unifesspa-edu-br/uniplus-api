namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

/// <summary>
/// Detecta <c>DbUpdateConcurrencyException</c> por <see cref="Type.FullName"/> (evita
/// dependência direta de <c>Microsoft.EntityFrameworkCore</c> na camada Application —
/// mantém Clean Arch, mesmo raciocínio de <see cref="ExclusionConstraintViolation"/>).
/// </summary>
/// <remarks>
/// O token de concorrência otimista (<c>xmin</c>, ver
/// <c>CalendarioDiasUteisConfiguration</c>) detecta a corrida entre
/// <c>MarcarVigenteCalendarioDiasUteisCommandHandler</c> e
/// <c>RemoverCalendarioDiasUteisCommandHandler</c> quando os dois mutam o MESMO
/// registro (ex.: ativar um dataset enquanto ele é removido) — corrida que a
/// exclusion constraint de vigência não cobre, por só comparar registros diferentes.
/// </remarks>
internal static class OptimisticConcurrencyViolation
{
    private const string DbUpdateConcurrencyExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException";

    public static bool Is(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return string.Equals(ex.GetType().FullName, DbUpdateConcurrencyExceptionFullName, StringComparison.Ordinal);
    }
}
