namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Linq;
using System.Text.Json;

using Entities;

using Enums;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

using ValueObjects;

/// <summary>
/// O veredicto de uma folha da árvore de satisfação: bloqueada pela fronteira de disponibilidade,
/// aplicável ou não pelo gatilho e satisfeita ou pendente pela cardinalidade das apresentações.
/// </summary>
internal static class ResolucaoDaFolha
{
    internal static EstadoSatisfacao Resolver(
        NoExigencia no,
        IReadOnlyDictionary<string, FatoResolvido> fatos,
        IReadOnlyDictionary<Guid, IReadOnlyList<ApresentacaoDocumento>> apresentacoes,
        IReadOnlySet<Guid> bloqueadas,
        Dictionary<Guid, StatusResolucaoExigencia> statusPorExigencia)
    {
        DocumentoExigido folha = no.DocumentoExigido!;

        // Fronteira de disponibilidade (Story #928, §6): enquanto o fato gatilho não alcançou a
        // fronteira (produtor não executado ou fase não satisfeita), a folha está BLOQUEADA. Projeta
        // INDETERMINADO — antes de avaliar o gatilho, exatamente como uma folha indeterminada —, e a
        // emissão é suprimida por emissionBlocked. Ao desbloquear (o conjunto deixa de contê-la),
        // reavalia normalmente, sem latch.
        if (bloqueadas.Contains(folha.Id))
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.AplicabilidadeIndeterminada;
            return EstadoSatisfacao.Indeterminado;
        }

        Ternario aplicavel = folha.AplicavelPara(fatos);

        // Story #916 (herdado): Indeterminado é resolvido fato-a-fato, SEMPRE — independente de já
        // existir apresentação (nunca vira Satisfeita só porque o candidato enviou algo).
        if (aplicavel == Ternario.Indeterminado)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.AplicabilidadeIndeterminada;
            return EstadoSatisfacao.Indeterminado;
        }

        if (aplicavel == Ternario.Falso)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.NaoAplicavel;
            return EstadoSatisfacao.NaoAplicavel;
        }

        // Story #921 — cardinalidade qualificada: quantidadeMinima conta apresentações (não
        // arquivos), qualificadas por chaveDistincao quando a folha declara slots (calendário
        // derivável ou ocorrências concretas) ou só distinção pura (Ocorrencia sem lista).
        int quantidadeMinima = no.QuantidadeMinima ?? NoExigencia.QuantidadeMinimaPadrao;
        // Dedup por IDENTIDADE uma vez, para os três modos — a mesma apresentação relatada
        // duas vezes (mesmo Id, ex.: join/merge duplicado a montante) nunca pode contribuir
        // duas vezes: nem para a contagem bruta, nem para duas tags de distinção pura, nem
        // para cobrir dois slots concretos diferentes (uma apresentação = um slot).
        List<ApresentacaoDocumento> apresentacoesDaFolha =
            [.. apresentacoes.GetValueOrDefault(folha.Id, []).DistinctBy(static a => a.Id)];
        bool satisfeita = no.ChaveDistincao switch
        {
            null => apresentacoesDaFolha.Count >= quantidadeMinima,
            // Distinção pura: tags nulas/em branco não distinguem nada — ignoradas antes do
            // Distinct, senão [null, "x"] contaria como 2 tags diferentes.
            ChaveDistincao.Ocorrencia when no.OcorrenciasEsperadas is null =>
                apresentacoesDaFolha
                    .Select(static a => a.ChaveDistincao)
                    .Where(static chave => !string.IsNullOrWhiteSpace(chave))
                    .Distinct(StringComparer.Ordinal)
                    .Count() >= quantidadeMinima,
            _ => no.SlotsEsperados()!.All(slot =>
                apresentacoesDaFolha.Any(a => a.ChaveDistincao == slot)),
        };

        if (satisfeita)
        {
            statusPorExigencia[folha.Id] = StatusResolucaoExigencia.Satisfeita;
            return EstadoSatisfacao.Satisfeito;
        }

        statusPorExigencia[folha.Id] = StatusResolucaoExigencia.Pendente;
        return EstadoSatisfacao.Pendente;
    }
}
