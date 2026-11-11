namespace Unifesspa.UniPlus.Selecao.Application.Commands;

/// <summary>
/// Detecta <c>DbUpdateConcurrencyException</c> pelo <see cref="Type.FullName"/>,
/// para que a camada Application não passe a depender do pacote do EF Core —
/// mesmo raciocínio dos helpers de violação de unicidade dos cadastros.
/// </summary>
/// <remarks>
/// A exceção chega quando o UPDATE amarrado ao <c>xmin</c> lido não encontra a
/// linha na versão esperada (ADR-0119): outra requisição mudou a mesma linha
/// entre a leitura e a gravação. Fica no namespace comum dos comandos porque
/// todo handler que captura o conflito para devolver falha (os que exigem
/// chave de idempotência) usa a mesma detecção.
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
