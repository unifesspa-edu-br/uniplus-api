namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O fato que gerou um aviso da reclassificação.</summary>
public enum OrigemDoAviso
{
    /// <summary>Uma permissão de perfil comum foi reclassificada para cima ou mudou de escopo.</summary>
    Reclassificacao = 1,

    /// <summary>Um perfil de governança divergiu do esperado.</summary>
    Divergencia = 2,
}

/// <summary>Código textual canônico de <see cref="OrigemDoAviso"/>, o valor gravado e exposto.</summary>
public static class OrigemDoAvisoCodigo
{
    public const string Reclassificacao = "reclassificacao";
    public const string Divergencia = "divergencia";

    public static string ToCodigo(this OrigemDoAviso valor) => valor switch
    {
        OrigemDoAviso.Reclassificacao => Reclassificacao,
        OrigemDoAviso.Divergencia => Divergencia,
        _ => throw new ArgumentOutOfRangeException(nameof(valor), valor, null),
    };

    public static OrigemDoAviso FromCodigo(string codigo) => codigo switch
    {
        Reclassificacao => OrigemDoAviso.Reclassificacao,
        Divergencia => OrigemDoAviso.Divergencia,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
