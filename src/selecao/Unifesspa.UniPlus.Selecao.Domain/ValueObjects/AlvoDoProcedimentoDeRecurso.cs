namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que um alvo de procedimento de recurso aponta: um item de avaliação da etapa ou uma área do ENEM.</summary>
public enum TipoDeAlvoDoProcedimentoDeRecurso
{
    [CodigoDeContrato("ITEM_AVALIACAO", "Item de avaliação")]
    ItemDeAvaliacao = 1,
    [CodigoDeContrato("AREA_ENEM", "Área do ENEM")]
    AreaDoEnem = 2,
}

/// <summary>
/// A parte da matéria que um procedimento de recurso cobre, quando ele não cobre a matéria inteira: um item de
/// avaliação, pela identidade que o snapshot congela, ou uma área do ENEM, pelo código canônico. A igualdade é
/// pelo valor.
/// </summary>
/// <remarks>
/// O alvo não sabe se existe: que o item pertença à etapa e que a área exista no processo é conferido por
/// <see cref="ConferenciaDosProcedimentosDeRecurso"/>, que enxerga o contexto.
/// </remarks>
public sealed record AlvoDoProcedimentoDeRecurso
{
    private AlvoDoProcedimentoDeRecurso(TipoDeAlvoDoProcedimentoDeRecurso tipo, string valor)
    {
        Tipo = tipo;
        Valor = valor;
    }

    public TipoDeAlvoDoProcedimentoDeRecurso Tipo { get; }

    /// <summary>O valor canônico: o identificador do item em forma "D" ou o código da área do ENEM.</summary>
    public string Valor { get; }

    public static AlvoDoProcedimentoDeRecurso DoItem(Guid itemDeAvaliacaoId) =>
        new(TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao, itemDeAvaliacaoId.ToString("D"));

    /// <exception cref="ArgumentException">O código da área está em branco.</exception>
    public static AlvoDoProcedimentoDeRecurso DaArea(string codigoDaArea)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDaArea);
        return new(TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem, codigoDaArea);
    }
}
