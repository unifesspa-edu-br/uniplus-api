namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação de uma versão do perfil de acesso.</summary>
public enum SituacaoDaVersao
{
    /// <summary>Criada e ainda sem efeito: espera o documento instituidor ser verificado.</summary>
    Pendente = 1,

    /// <summary>Composição em vigor do perfil. Há no máximo uma por perfil.</summary>
    Vigente = 2,

    /// <summary>Foi a vigente e deu lugar a uma versão posterior.</summary>
    Encerrada = 3,

    /// <summary>Terminal: retirada antes de entrar em vigor, por quem a criou, por quem mantém perfis ou pela contenção.</summary>
    Cancelada = 4,

    /// <summary>Terminal: uma etapa da aprovação a recusou.</summary>
    Recusada = 5,

    /// <summary>Terminal: o prazo de 15 dias passou sem a aprovação.</summary>
    Expirada = 6,
}

/// <summary>Código textual canônico de <see cref="SituacaoDaVersao"/>, o valor gravado e exposto.</summary>
public static class SituacaoDaVersaoCodigo
{
    public const string Pendente = "pendente";
    public const string Vigente = "vigente";
    public const string Encerrada = "encerrada";
    public const string Cancelada = "cancelada";
    public const string Recusada = "recusada";
    public const string Expirada = "expirada";

    public static string ToCodigo(this SituacaoDaVersao situacao) => situacao switch
    {
        SituacaoDaVersao.Pendente => Pendente,
        SituacaoDaVersao.Vigente => Vigente,
        SituacaoDaVersao.Encerrada => Encerrada,
        SituacaoDaVersao.Cancelada => Cancelada,
        SituacaoDaVersao.Recusada => Recusada,
        SituacaoDaVersao.Expirada => Expirada,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDaVersao FromCodigo(string codigo) => codigo switch
    {
        Pendente => SituacaoDaVersao.Pendente,
        Vigente => SituacaoDaVersao.Vigente,
        Encerrada => SituacaoDaVersao.Encerrada,
        Cancelada => SituacaoDaVersao.Cancelada,
        Recusada => SituacaoDaVersao.Recusada,
        Expirada => SituacaoDaVersao.Expirada,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
