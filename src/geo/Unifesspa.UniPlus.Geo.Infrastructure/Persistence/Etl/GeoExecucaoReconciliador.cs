namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Reconcilia execuções do ETL abandonadas por crash/restart (Story #674): marca
/// <c>Falhou</c> as que estão <c>EmAndamento</c> há mais que o limite de abandono,
/// liberando o índice único parcial para novos disparos.
/// </summary>
/// <remarks>
/// O filtro por idade (<c>iniciado_em &lt; agora - limite</c>) é deliberado: numa topologia
/// multi-réplica, uma carga recém-iniciada em outra instância tem <c>iniciado_em</c> recente
/// e <strong>não</strong> é reclamada — só execuções antigas o bastante para serem
/// seguramente órfãs. Single-flight estrito (uma carga por vez no cluster) é garantido pelo
/// índice único parcial no banco; correção total de recuperação multi-réplica (lease/heartbeat)
/// fica como evolução futura — o limite folgado cobre o caso comum de operação.
/// </remarks>
internal static class GeoExecucaoReconciliador
{
    public static Task<int> ReclamarAbandonadasAsync(
        GeoDbContext contexto,
        DateTimeOffset agora,
        TimeSpan limiteAbandono,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        DateTimeOffset limite = agora - limiteAbandono;

        return contexto.Set<GeoImportacaoExecucao>()
            .Where(e => e.Status == StatusImportacao.EmAndamento && e.IniciadoEm < limite)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.Status, StatusImportacao.Falhou)
                    .SetProperty(e => e.ConcluidoEm, agora)
                    .SetProperty(e => e.Mensagem, "Carga reconciliada como abandonada (sem conclusão além do limite).")
                    .SetProperty(e => e.UpdatedAt, agora),
                cancellationToken);
    }
}
