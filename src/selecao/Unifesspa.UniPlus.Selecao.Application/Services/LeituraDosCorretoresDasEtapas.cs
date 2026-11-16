namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Bancas;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ValueObjects;

/// <summary>
/// Lê no rol o número de corretores da regra de consolidação que cada etapa do processo referencia. O Domain não lê o
/// catálogo (ADR-0042), então o número chega pronto. O congelamento e a consulta de conformidade usam esta mesma
/// leitura: o painel avalia as bancas com o conjunto que a publicação avalia.
/// </summary>
/// <remarks>
/// Cada referência distinta é lida uma vez, ainda que várias etapas a compartilhem. A etapa sem regra de consolidação
/// não consulta o catálogo. A referência que o catálogo não confirma, por regra ausente, por hash divergente ou por
/// versão que não declara o número de corretores de forma válida, não entra no conjunto: o número dela não se presume, e quem calcula o mínimo das bancas a recusa como regra não
/// resolvida. A falha de leitura do banco segue como exceção.
/// </remarks>
public sealed class LeituraDosCorretoresDasEtapas(IRegraCatalogoReader regras)
{
    internal async Task<CorretoresDasEtapas> LerAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        Dictionary<ReferenciaRegra, int> porRegra = [];
        foreach (ReferenciaRegra referencia in processo.Etapas.Select(static etapa => etapa.RegraConsolidacao).OfType<ReferenciaRegra>().Distinct())
        {
            if (await LerDaRegraAsync(referencia, cancellationToken).ConfigureAwait(false) is { } corretores)
            {
                porRegra[referencia] = corretores;
            }
        }

        return new CorretoresDasEtapas(porRegra);
    }

    private async Task<int?> LerDaRegraAsync(ReferenciaRegra referencia, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regras.ObterAsync(referencia.Codigo, referencia.Versao, cancellationToken).ConfigureAwait(false);

        return regra?.Referencia() == referencia && CorretoresDaRegraDeConsolidacao.Tentar(regra, out int corretores) ? corretores : null;
    }
}
