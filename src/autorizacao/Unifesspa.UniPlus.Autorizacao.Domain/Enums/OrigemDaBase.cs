namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>De onde vem a base de uma permissão na concessão: a classificação e o escopo contra os quais a reclassificação se mede.</summary>
public enum OrigemDaBase
{
    /// <summary>A concessão trocou de versão e manteve a permissão: a base é o retrato da versão que saiu.</summary>
    Migracao = 1,

    /// <summary>A permissão saiu da composição da concessão: encerra a base dela.</summary>
    Saida = 2,

    /// <summary>As etapas exigidas liberaram a reclassificação: a base passa a ser a do catálogo da reclassificação.</summary>
    Liberacao = 3,

    /// <summary>Uma troca de catálogo mudou a permissão: a base acompanha a descida da classe e guarda o escopo anterior.</summary>
    Conciliacao = 4,
}

/// <summary>Código textual canônico de <see cref="OrigemDaBase"/>, o valor gravado e exposto.</summary>
public static class OrigemDaBaseCodigo
{
    public const string Migracao = "migracao";
    public const string Saida = "saida";
    public const string Liberacao = "liberacao";
    public const string Conciliacao = "conciliacao";

    public static string ToCodigo(this OrigemDaBase valor) => valor switch
    {
        OrigemDaBase.Migracao => Migracao,
        OrigemDaBase.Saida => Saida,
        OrigemDaBase.Liberacao => Liberacao,
        OrigemDaBase.Conciliacao => Conciliacao,
        _ => throw new ArgumentOutOfRangeException(nameof(valor), valor, null),
    };

    public static OrigemDaBase FromCodigo(string codigo) => codigo switch
    {
        Migracao => OrigemDaBase.Migracao,
        Saida => OrigemDaBase.Saida,
        Liberacao => OrigemDaBase.Liberacao,
        Conciliacao => OrigemDaBase.Conciliacao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
