namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Por que os gestores da instalação receberam a concessão sem aceitar o termo de responsabilidade.</summary>
public enum SituacaoDoTermoNaInstalacao
{
    /// <summary>A instalação antecedeu a primeira publicação do termo: não havia termo a aceitar.</summary>
    SemTermoPublicado = 1,

    /// <summary>Estado anômalo: já havia versão vigente, e a instalação a registrou sem exigir aceite (ADR-0162).</summary>
    TermoVigenteSemAceite = 2,
}

/// <summary>Código textual canônico de <see cref="SituacaoDoTermoNaInstalacao"/>, o valor gravado e exposto.</summary>
public static class SituacaoDoTermoNaInstalacaoCodigo
{
    public const string SemTermoPublicado = "sem_termo_publicado";
    public const string TermoVigenteSemAceite = "termo_vigente_sem_aceite";

    public static string ToCodigo(this SituacaoDoTermoNaInstalacao situacao) => situacao switch
    {
        SituacaoDoTermoNaInstalacao.SemTermoPublicado => SemTermoPublicado,
        SituacaoDoTermoNaInstalacao.TermoVigenteSemAceite => TermoVigenteSemAceite,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDoTermoNaInstalacao FromCodigo(string codigo) => codigo switch
    {
        SemTermoPublicado => SituacaoDoTermoNaInstalacao.SemTermoPublicado,
        TermoVigenteSemAceite => SituacaoDoTermoNaInstalacao.TermoVigenteSemAceite,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
