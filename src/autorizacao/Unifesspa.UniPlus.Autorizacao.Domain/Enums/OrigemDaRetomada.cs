namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que levou o sistema a fechar a suspensão.</summary>
public enum OrigemDaRetomada
{
    /// <summary>A decisão de uma etapa completou as exigidas.</summary>
    DecisaoDaEtapa = 1,

    /// <summary>Uma versão entrou em vigor e o perfil deixou de exigir a etapa que faltava.</summary>
    TrocaDeVigencia = 2,

    /// <summary>A reclassificação de uma permissão tirou do perfil a classe que exigia a etapa que faltava.</summary>
    Reclassificacao = 3,
}

/// <summary>Código textual canônico de <see cref="OrigemDaRetomada"/>, o valor gravado e exposto.</summary>
public static class OrigemDaRetomadaCodigo
{
    public const string DecisaoDaEtapa = "decisao_da_etapa";
    public const string TrocaDeVigencia = "troca_de_vigencia";
    public const string Reclassificacao = "reclassificacao";

    public static string ToCodigo(this OrigemDaRetomada origem) => origem switch
    {
        OrigemDaRetomada.DecisaoDaEtapa => DecisaoDaEtapa,
        OrigemDaRetomada.TrocaDeVigencia => TrocaDeVigencia,
        OrigemDaRetomada.Reclassificacao => Reclassificacao,
        _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, null),
    };

    public static OrigemDaRetomada FromCodigo(string codigo) => codigo switch
    {
        DecisaoDaEtapa => OrigemDaRetomada.DecisaoDaEtapa,
        TrocaDeVigencia => OrigemDaRetomada.TrocaDeVigencia,
        Reclassificacao => OrigemDaRetomada.Reclassificacao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
