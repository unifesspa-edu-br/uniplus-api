namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Desfecho de uma tentativa de confirmar a instalação com o código, ou o reenvio do código.</summary>
public enum ResultadoDaTentativaDeInstalacao
{
    /// <summary>Código certo, dentro do prazo, da pessoa que iniciou.</summary>
    Aceita = 1,

    CodigoErrado = 2,

    /// <summary>Código informado depois do prazo: a instalação expirou.</summary>
    Expirada = 3,

    /// <summary>Quem tentou não tem instalação aguardando código.</summary>
    SemPendencia = 4,

    /// <summary>Código certo, mas outra instalação já deixou gestor ativo: nada foi confirmado.</summary>
    JaHaviaGestor = 5,

    /// <summary>Novo código enviado a quem iniciou, que invalida o anterior; não é uma tentativa de confirmar.</summary>
    CodigoReenviado = 6,
}

/// <summary>Código textual canônico de <see cref="ResultadoDaTentativaDeInstalacao"/>, o valor gravado e exposto.</summary>
public static class ResultadoDaTentativaDeInstalacaoCodigo
{
    public const string Aceita = "aceita";
    public const string CodigoErrado = "codigo_errado";
    public const string Expirada = "expirada";
    public const string SemPendencia = "sem_pendencia";
    public const string JaHaviaGestor = "ja_havia_gestor";
    public const string CodigoReenviado = "codigo_reenviado";

    public static string ToCodigo(this ResultadoDaTentativaDeInstalacao resultado) => resultado switch
    {
        ResultadoDaTentativaDeInstalacao.Aceita => Aceita,
        ResultadoDaTentativaDeInstalacao.CodigoErrado => CodigoErrado,
        ResultadoDaTentativaDeInstalacao.Expirada => Expirada,
        ResultadoDaTentativaDeInstalacao.SemPendencia => SemPendencia,
        ResultadoDaTentativaDeInstalacao.JaHaviaGestor => JaHaviaGestor,
        ResultadoDaTentativaDeInstalacao.CodigoReenviado => CodigoReenviado,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    public static ResultadoDaTentativaDeInstalacao FromCodigo(string codigo) => codigo switch
    {
        Aceita => ResultadoDaTentativaDeInstalacao.Aceita,
        CodigoErrado => ResultadoDaTentativaDeInstalacao.CodigoErrado,
        Expirada => ResultadoDaTentativaDeInstalacao.Expirada,
        SemPendencia => ResultadoDaTentativaDeInstalacao.SemPendencia,
        JaHaviaGestor => ResultadoDaTentativaDeInstalacao.JaHaviaGestor,
        CodigoReenviado => ResultadoDaTentativaDeInstalacao.CodigoReenviado,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
