namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os parâmetros de cada eliminação declarada, na posição em que ela veio no pedido. A posição fica vazia onde o
/// item foi recusado antes de ter parâmetros montados: as conferências da lista olham só os itens montados, mas a
/// quantidade conta todos os declarados.
/// </summary>
public sealed record EliminacoesDeclaradas
{
    private readonly IReadOnlyList<ParametrosRegraEliminacao?> _parametrosPorPosicao;

    private EliminacoesDeclaradas(IReadOnlyList<ParametrosRegraEliminacao?> parametrosPorPosicao) =>
        _parametrosPorPosicao = parametrosPorPosicao;

    /// <summary>Quantas eliminações o pedido declarou, as recusadas inclusive.</summary>
    public int Quantidade => _parametrosPorPosicao.Count;

    /// <summary>Os itens com parâmetros montados, cada um com a posição em que veio.</summary>
    public IEnumerable<(int Posicao, ParametrosRegraEliminacao Parametros)> Montadas =>
        _parametrosPorPosicao
            .Select(static (parametros, posicao) => (Posicao: posicao, Parametros: parametros))
            .Where(static item => item.Parametros is not null)
            .Select(static item => (item.Posicao, item.Parametros!));

    /// <summary>Uma posição por item do pedido, com os parâmetros montados ou nulos onde o item foi recusado.</summary>
    public static EliminacoesDeclaradas NasPosicoes(IReadOnlyList<ParametrosRegraEliminacao?> parametrosPorPosicao)
    {
        ArgumentNullException.ThrowIfNull(parametrosPorPosicao);

        return new(parametrosPorPosicao);
    }

    /// <summary>As eliminações já criadas: todas as posições têm parâmetros.</summary>
    public static EliminacoesDeclaradas DasRegras(IReadOnlyList<RegraEliminacao> regras)
    {
        ArgumentNullException.ThrowIfNull(regras);

        return new([.. regras.Select(static regra => (ParametrosRegraEliminacao?)regra.Parametros)]);
    }
}
