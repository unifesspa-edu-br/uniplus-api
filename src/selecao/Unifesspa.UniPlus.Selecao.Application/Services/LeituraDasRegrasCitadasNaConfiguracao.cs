namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Domain.ValueObjects;

/// <summary>Lê as regras citadas para a conferência e para a resolução dos corretores.</summary>
public sealed class LeituraDasRegrasCitadasNaConfiguracao(IRegraCatalogoReader regras)
{
    internal async Task<RegrasCitadasNaConfiguracao> LerAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);

        Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> encontradas = [];
        Dictionary<ReferenciaRegra, CorretoresDaRegra> corretores = [];
        await LerRegrasAsync(processo, encontradas, corretores, cancellationToken).ConfigureAwait(false);
        return new RegrasCitadasNaConfiguracao(encontradas, new CorretoresDasEtapas(corretores));
    }

    private async Task LerRegrasAsync(
        ProcessoSeletivo processo, Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> encontradas,
        Dictionary<ReferenciaRegra, CorretoresDaRegra> corretores, CancellationToken cancellationToken)
    {
        foreach ((string Codigo, string Versao) chave in Chaves(processo))
        {
            RegraCatalogo? regra = await regras.ObterAsync(chave.Codigo, chave.Versao, cancellationToken).ConfigureAwait(false);
            if (regra is not null)
            {
                encontradas[chave] = new RegraDoCatalogoCitada(regra.Tipo, regra.Referencia(), regra.DeclaracaoDaRegra);
                AdicionarCorretores(regra, corretores);
            }
        }
    }

    private static IEnumerable<(string Codigo, string Versao)> Chaves(ProcessoSeletivo processo) =>
        ReferenciasDeRegraDaConfiguracao.De(processo.Declaracao())
            .Select(static item => (item.Regra.Codigo, item.Regra.Versao)).Distinct();

    private static void AdicionarCorretores(RegraCatalogo regra, Dictionary<ReferenciaRegra, CorretoresDaRegra> corretores)
    {
        if (CorretoresDaRegraDeConsolidacao.Tentar(regra, out int distintos)
            && CorretoresDaRegraDeConsolidacao.TentarEmCampo(regra, out int emCampo))
        {
            corretores[regra.Referencia()] = new CorretoresDaRegra(distintos, emCampo);
        }
    }
}

internal sealed record RegrasCitadasNaConfiguracao(
    IReadOnlyDictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras,
    CorretoresDasEtapas Corretores);
