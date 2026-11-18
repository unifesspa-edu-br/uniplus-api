namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Globalization;
using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// O bloqueio de uma linha, com espera limitada: esgotado o limite, a operação que esperava é recusada em vez de ficar
/// presa pelo tempo do driver.
/// </summary>
internal sealed class BloqueioDeLinhaComEsperaLimitada(UniPlusDbContext context, TravaDoProcessoOptions opcoes)
{
    public async Task AdquirirAsync(InstrucaoDeBloqueio instrucao, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(BloquearComLimiteDeEspera(instrucao, id), cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException falha) when (falha.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new EsperaPelaTravaEsgotadaException(falha);
        }
    }

    // O limite vale só enquanto se espera a linha: o último set_config devolve o que a transação já tinha para os bloqueios
    // seguintes, cuja espera esgotada não é convertida. Os comandos vão num só envio ao banco; o limite é {0} e o
    // identificador da linha é {1}.
    private FormattableString BloquearComLimiteDeEspera(InstrucaoDeBloqueio instrucao, Guid id)
    {
        string limite = opcoes.EsperaMaximaEmMilissegundos.ToString(CultureInfo.InvariantCulture);
        string comandos = $$"""
            SELECT set_config('uniplus.lock_timeout_anterior', current_setting('lock_timeout'), true);
            SELECT set_config('lock_timeout', {0}, true);
            {{instrucao.Comando}};
            SELECT set_config('lock_timeout', current_setting('uniplus.lock_timeout_anterior'), true)
            """;
        return FormattableStringFactory.Create(comandos, limite, id);
    }
}
