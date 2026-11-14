namespace Unifesspa.UniPlus.Configuracao.Application.Persistence;

/// <summary>
/// Reconhece a corrida de duas edições que substituem a mesma coleção de linhas filhas (valores do tipo de
/// conceito, conceitos admitidos do tipo de etapa). O EF envia a substituição num lote só: a edição perdedora
/// insere as linhas dela antes de conferir que as antigas já tinham sido apagadas, e o banco recusa o lote pelo
/// índice único das linhas que a vencedora gravou. Como a coleção chega validada do domínio, a violação desses
/// índices só pode vir da corrida; qualquer outra violação única segue como falha.
/// </summary>
internal static class ColisaoDeSubstituicaoConcorrente
{
    private const string UniqueViolationSqlState = "23505";
    private const string DbUpdateExceptionFullName = "Microsoft.EntityFrameworkCore.DbUpdateException";

    private static readonly string[] RestricoesDasLinhasSubstituidas =
    [
        RestricoesDoBanco.TipoDeConceitoValorCodigoUnico,
        RestricoesDoBanco.TipoDeConceitoValorRotuloUnico,
        RestricoesDoBanco.TipoDeConceitoValorPosicaoUnica,
        RestricoesDoBanco.TipoDeEtapaConceitoAdmitidoChave,
    ];

    /// <remarks>
    /// O tipo da exceção é inspecionado por nome para a camada Application não depender do EF nem do Npgsql,
    /// como nos demais tradutores de violação do módulo.
    /// </remarks>
    public static bool Ocorreu(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!string.Equals(exception.GetType().FullName, DbUpdateExceptionFullName, StringComparison.Ordinal)
            || exception.InnerException is not { } interna)
        {
            return false;
        }

        Type tipo = interna.GetType();
        return string.Equals(tipo.GetProperty("SqlState")?.GetValue(interna) as string, UniqueViolationSqlState, StringComparison.Ordinal)
            && tipo.GetProperty("ConstraintName")?.GetValue(interna) is string restricao
            && RestricoesDasLinhasSubstituidas.Contains(restricao, StringComparer.Ordinal);
    }
}
