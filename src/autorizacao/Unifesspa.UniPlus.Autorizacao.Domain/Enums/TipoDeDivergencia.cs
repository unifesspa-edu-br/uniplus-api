namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o perfil de governança tem de diferente do esperado pelo código ou do catálogo anterior.</summary>
public enum TipoDeDivergencia
{
    /// <summary>O código espera uma permissão que o perfil não reúne.</summary>
    ComposicaoFaltante = 1,

    /// <summary>O perfil reúne uma permissão que o código não espera.</summary>
    ComposicaoExcedente = 2,

    /// <summary>A classificação de uma permissão do perfil mudou no catálogo.</summary>
    Classificacao = 3,

    /// <summary>O escopo de uma permissão do perfil mudou no catálogo.</summary>
    Escopo = 4,
}

/// <summary>Código textual canônico de <see cref="TipoDeDivergencia"/>, o valor gravado e exposto.</summary>
public static class TipoDeDivergenciaCodigo
{
    public const string ComposicaoFaltante = "composicao_faltante";
    public const string ComposicaoExcedente = "composicao_excedente";
    public const string Classificacao = "classificacao";
    public const string Escopo = "escopo";

    public static string ToCodigo(this TipoDeDivergencia valor) => valor switch
    {
        TipoDeDivergencia.ComposicaoFaltante => ComposicaoFaltante,
        TipoDeDivergencia.ComposicaoExcedente => ComposicaoExcedente,
        TipoDeDivergencia.Classificacao => Classificacao,
        TipoDeDivergencia.Escopo => Escopo,
        _ => throw new ArgumentOutOfRangeException(nameof(valor), valor, null),
    };

    public static TipoDeDivergencia FromCodigo(string codigo) => codigo switch
    {
        ComposicaoFaltante => TipoDeDivergencia.ComposicaoFaltante,
        ComposicaoExcedente => TipoDeDivergencia.ComposicaoExcedente,
        Classificacao => TipoDeDivergencia.Classificacao,
        Escopo => TipoDeDivergencia.Escopo,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
