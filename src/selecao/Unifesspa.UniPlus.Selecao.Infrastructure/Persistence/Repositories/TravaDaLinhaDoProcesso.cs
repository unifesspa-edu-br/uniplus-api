namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A trava exclusiva da linha do processo, com espera limitada: esgotado o limite, a operação que esperava é recusada
/// em vez de ficar presa pelo tempo do driver.
/// </summary>
internal sealed class TravaDaLinhaDoProcesso(UniPlusDbContext context, TravaDoProcessoOptions opcoes)
{
    public async Task AdquirirAsync(Guid processoId, CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(TravarComLimiteDeEspera(processoId), cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException falha) when (falha.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new EsperaPelaTravaEsgotadaException(falha);
        }
    }

    // O limite vale só enquanto se espera a linha do processo: o último set_config devolve o que a transação já tinha
    // para as travas seguintes, cuja espera esgotada não é convertida. Os comandos vão num só envio ao banco.
    private FormattableString TravarComLimiteDeEspera(Guid processoId)
    {
        string limite = opcoes.EsperaMaximaEmMilissegundos.ToString(CultureInfo.InvariantCulture);
        return $"""
            SELECT set_config('uniplus.lock_timeout_anterior', current_setting('lock_timeout'), true);
            SELECT set_config('lock_timeout', {limite}, true);
            SELECT 1 FROM selecao.processos_seletivos WHERE id = {processoId} FOR UPDATE;
            SELECT set_config('lock_timeout', current_setting('uniplus.lock_timeout_anterior'), true)
            """;
    }
}
