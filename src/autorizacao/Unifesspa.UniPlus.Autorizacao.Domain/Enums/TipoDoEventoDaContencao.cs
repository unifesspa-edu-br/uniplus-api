namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que a trilha da contenção registra do perfil.</summary>
public enum TipoDoEventoDaContencao
{
    /// <summary>Uma pessoa suspendeu o perfil.</summary>
    Suspensao = 1,

    /// <summary>A suspensão se fechou: as etapas exigidas foram decididas.</summary>
    Retomada = 2,

    /// <summary>Uma pessoa desativou o perfil.</summary>
    Desativacao = 3,

    /// <summary>Outra pessoa reativou o perfil desativado.</summary>
    Reativacao = 4,
}

/// <summary>Código textual canônico de <see cref="TipoDoEventoDaContencao"/>, o valor gravado e exposto.</summary>
public static class TipoDoEventoDaContencaoCodigo
{
    public const string Suspensao = "suspensao";
    public const string Retomada = "retomada";
    public const string Desativacao = "desativacao";
    public const string Reativacao = "reativacao";

    public static string ToCodigo(this TipoDoEventoDaContencao tipo) => tipo switch
    {
        TipoDoEventoDaContencao.Suspensao => Suspensao,
        TipoDoEventoDaContencao.Retomada => Retomada,
        TipoDoEventoDaContencao.Desativacao => Desativacao,
        TipoDoEventoDaContencao.Reativacao => Reativacao,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoDoEventoDaContencao FromCodigo(string codigo) => codigo switch
    {
        Suspensao => TipoDoEventoDaContencao.Suspensao,
        Retomada => TipoDoEventoDaContencao.Retomada,
        Desativacao => TipoDoEventoDaContencao.Desativacao,
        Reativacao => TipoDoEventoDaContencao.Reativacao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
