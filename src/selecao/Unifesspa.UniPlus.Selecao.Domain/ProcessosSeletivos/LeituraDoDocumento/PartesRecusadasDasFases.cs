namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// As partes recusadas das fases como a conferência as conhece: a parte de uma fase adotada pelo caminho com a posição canônica dela
/// (<c>fases[1].unidadeResponsavel</c>) e a fase que a leitura recusou pelo nível da lista (<c>fases</c>). A causa aponta o documento
/// pela posição de origem, convertida pelo conversor único (<see cref="PonteiroNoDocumento"/>). Saem na ordem em que o documento traz as fases.
/// </summary>
internal static class PartesRecusadasDasFases
{
    internal static IReadOnlyList<ParteRecusada> De(IReadOnlyList<FaseLida> lidas, OrigemNoDocumento posicoes)
    {
        Dictionary<int, int> canonicaPorOrigem = PosicoesCanonicas(lidas, posicoes);

        return
        [
            .. lidas.SelectMany(lida => lida.Recusas.Select(recusa => lida.Fase is null
                ? DaFaseRecusada(lida, recusa)
                : DaParteDaFase(recusa, canonicaPorOrigem[lida.PosicaoNoDocumento], posicoes))),
        ];
    }

    /// <summary>A posição canônica de cada fase adotada, pela posição em que veio no documento.</summary>
    internal static Dictionary<int, int> PosicoesCanonicas(IReadOnlyList<FaseLida> lidas, OrigemNoDocumento posicoes) =>
        Enumerable.Range(0, lidas.Count(static lida => lida.Fase is not null))
            .ToDictionary(canonica => posicoes.PosicaoDeOrigem(CronogramaDoProcesso.ListaDasFases, canonica));

    private static ParteRecusada DaFaseRecusada(FaseLida lida, RecusaDaParte recusa) => new(
        CronogramaDoProcesso.ListaDasFases,
        lida.Chave,
        lida.PosicaoNoDocumento,
        Causa($"fases[{lida.PosicaoNoDocumento}].{recusa.Campo}", recusa.Erro, OrigemNoDocumento.Identidade));

    private static ParteRecusada DaParteDaFase(RecusaDaParte recusa, int posicaoCanonica, OrigemNoDocumento posicoes) => new(
        $"fases[{posicaoCanonica}].{recusa.Parte}",
        recusa.Chave,
        recusa.PosicaoNoDocumento,
        Causa($"fases[{posicaoCanonica}].{recusa.Campo}", recusa.Erro, posicoes));

    private static FieldError Causa(string campo, DomainError erro, OrigemNoDocumento origem) =>
        new(PonteiroNoDocumento.Converter(campo, origem), erro);
}
