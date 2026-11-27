namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// As partes recusadas das etapas como a conferência as conhece: a parte de uma etapa adotada pelo caminho com a posição canônica
/// dela (<c>etapas[1].bancas</c>) e a etapa que a leitura recusou pelo nível da lista (<c>etapas</c>). A causa aponta o documento
/// pela posição de origem, convertida pelo conversor único (<see cref="PonteiroNoDocumento"/>). Saem na ordem em que o documento
/// traz as etapas.
/// </summary>
internal static class PartesRecusadasDasEtapas
{
    internal static IReadOnlyList<ParteRecusada> De(IReadOnlyList<EtapaLida> lidas, OrigemNoDocumento posicoes)
    {
        Dictionary<int, int> canonicaPorOrigem = PosicoesCanonicas(lidas, posicoes);

        return
        [
            .. lidas.SelectMany(lida => lida.Recusas.Select(recusa => lida.Etapa is null
                ? DaEtapaRecusada(lida, recusa)
                : DaParteDaEtapa(recusa, canonicaPorOrigem[lida.PosicaoNoDocumento], posicoes))),
        ];
    }

    /// <summary>A posição canônica de cada etapa adotada, pela posição em que veio no documento.</summary>
    internal static Dictionary<int, int> PosicoesCanonicas(IReadOnlyList<EtapaLida> lidas, OrigemNoDocumento posicoes) =>
        Enumerable.Range(0, lidas.Count(static lida => lida.Etapa is not null))
            .ToDictionary(canonica => posicoes.PosicaoDeOrigem(ListasDasEtapas.ListaDasEtapas, canonica));

    private static ParteRecusada DaEtapaRecusada(EtapaLida lida, RecusaDaParte recusa) => new(
        ListasDasEtapas.ListaDasEtapas,
        lida.Chave,
        lida.PosicaoNoDocumento,
        Causa(new FieldError(recusa.Campo, recusa.Erro).NoCaminhoDe($"{ListasDasEtapas.ListaDasEtapas}[{lida.PosicaoNoDocumento}]"), OrigemNoDocumento.Identidade));

    private static ParteRecusada DaParteDaEtapa(RecusaDaParte recusa, int posicaoCanonica, OrigemNoDocumento posicoes) => new(
        $"{ListasDasEtapas.ListaDasEtapas}[{posicaoCanonica}].{recusa.Parte}",
        recusa.Chave,
        recusa.PosicaoNoDocumento,
        Causa(new FieldError(recusa.Campo, recusa.Erro).NoCaminhoDe($"{ListasDasEtapas.ListaDasEtapas}[{posicaoCanonica}]"), posicoes));

    private static FieldError Causa(FieldError causa, OrigemNoDocumento origem) =>
        causa with { Field = PonteiroNoDocumento.Converter(causa.Field, origem) };
}
