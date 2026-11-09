namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Como a versão nasceu: por uma proposta que espera aprovação ou por uma remoção, que vale na hora.</summary>
public enum OrigemDaVersao
{
    /// <summary>Criada por quem mantém perfis, pendente até a aprovação.</summary>
    Proposta = 1,

    /// <summary>Criada pela remoção de permissões: entra em vigor sem aprovação.</summary>
    Remocao = 2,
}

/// <summary>Código textual canônico de <see cref="OrigemDaVersao"/>, o valor gravado e exposto.</summary>
public static class OrigemDaVersaoCodigo
{
    public const string Proposta = "proposta";
    public const string Remocao = "remocao";

    public static string ToCodigo(this OrigemDaVersao origem) => origem switch
    {
        OrigemDaVersao.Proposta => Proposta,
        OrigemDaVersao.Remocao => Remocao,
        _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, null),
    };

    public static OrigemDaVersao FromCodigo(string codigo) => codigo switch
    {
        Proposta => OrigemDaVersao.Proposta,
        Remocao => OrigemDaVersao.Remocao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
