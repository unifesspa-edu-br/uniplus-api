namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

/// <summary>
/// Detecta <c>DbUpdateConcurrencyException</c> pelo <see cref="Type.FullName"/>,
/// para que a camada Application não passe a depender do pacote do EF Core —
/// mesmo raciocínio do helper de violação de unicidade ao lado.
/// </summary>
/// <remarks>
/// A exceção chega quando o UPDATE amarrado ao <c>xmin</c> lido não encontra a
/// linha na versão esperada (ADR-0119), o que aqui significa que outra
/// requisição mudou a situação do motivo entre a leitura e a gravação.
/// </remarks>
internal static class OptimisticConcurrencyViolation
{
    private const string DbUpdateConcurrencyExceptionFullName =
        "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException";

    public static bool Is(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return string.Equals(
            ex.GetType().FullName,
            DbUpdateConcurrencyExceptionFullName,
            StringComparison.Ordinal);
    }
}
