namespace Unifesspa.UniPlus.Regras.ValueObjects;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O resultado da derivação de um fato (Story #927): o estado e, quando resolvido, o conjunto de
/// códigos contribuídos pelas regras ativas.
/// </summary>
/// <remarks>
/// Um fato derivado é <see cref="EstadoFato.Indeterminado"/> quando algum dependente ainda não se
/// sabe (fail-closed), e <see cref="EstadoFato.Resolvido"/> caso contrário — com um conjunto que pode
/// ser vazio (nenhuma regra ativa). Não há estado não-aplicável no derivado: a inaplicabilidade de um
/// dependente já foi absorvida pela avaliação das regras.
/// </remarks>
public sealed record ResultadoDerivacao
{
    private ResultadoDerivacao(EstadoFato estado, IReadOnlySet<string> valores, bool? valorBooleano = null)
    {
        Estado = estado;
        Valores = valores;
        ValorBooleano = valorBooleano;
    }

    /// <summary>O valor do derivado booleano resolvido; nulo no categórico e no indeterminado.</summary>
    public bool? ValorBooleano { get; }

    /// <summary>O derivado booleano é conhecido: verdadeiro se alguma regra ativou.</summary>
    public static ResultadoDerivacao ResolvidoBooleano(bool valor) =>
        new(EstadoFato.Resolvido, FrozenSet<string>.Empty, valor);

    public EstadoFato Estado { get; }

    /// <summary>O conjunto derivado — vazio quando indeterminado ou quando nenhuma regra ativou.</summary>
    public IReadOnlySet<string> Valores { get; }

    /// <summary>O derivado ainda não é conhecido porque um dependente é indeterminado.</summary>
    public static ResultadoDerivacao Indeterminado { get; } =
        new(EstadoFato.Indeterminado, FrozenSet<string>.Empty);

    /// <summary>
    /// O derivado é conhecido — o conjunto é a união das contribuições ativas. O conjunto é
    /// congelado na construção: <see cref="Valores"/> é imutável e não compartilha referência com o
    /// que o motor montou, então nenhum consumidor pode contaminar o resultado por um cast.
    /// </summary>
    public static ResultadoDerivacao Resolvido(IReadOnlySet<string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        return new(EstadoFato.Resolvido, valores.ToFrozenSet(StringComparer.Ordinal));
    }
}
