namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que uma decisão da janela de migração fez com a concessão.</summary>
public enum ResultadoDaMigracao
{
    Migrar = 1,
    Recusar = 2,

    /// <summary>O sistema reteve a concessão do próprio gestor que enviou o lote.</summary>
    Reter = 3,

    /// <summary>O sistema encerrou a concessão que ninguém decidiu até o fim da janela.</summary>
    NaoMigrada = 4,

    /// <summary>O sistema tirou da lista a concessão que foi revogada ou venceu antes da decisão.</summary>
    ForaDaLista = 5,
}

/// <summary>Código textual canônico de <see cref="ResultadoDaMigracao"/>, o valor gravado e exposto.</summary>
public static class ResultadoDaMigracaoCodigo
{
    public const string Migrar = "migrar";
    public const string Recusar = "recusar";
    public const string Reter = "reter";
    public const string NaoMigrada = "nao_migrada";
    public const string ForaDaLista = "fora_da_lista";

    public static string ToCodigo(this ResultadoDaMigracao resultado) => resultado switch
    {
        ResultadoDaMigracao.Migrar => Migrar,
        ResultadoDaMigracao.Recusar => Recusar,
        ResultadoDaMigracao.Reter => Reter,
        ResultadoDaMigracao.NaoMigrada => NaoMigrada,
        ResultadoDaMigracao.ForaDaLista => ForaDaLista,
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, null),
    };

    public static ResultadoDaMigracao FromCodigo(string codigo) => codigo switch
    {
        Migrar => ResultadoDaMigracao.Migrar,
        Recusar => ResultadoDaMigracao.Recusar,
        Reter => ResultadoDaMigracao.Reter,
        NaoMigrada => ResultadoDaMigracao.NaoMigrada,
        ForaDaLista => ResultadoDaMigracao.ForaDaLista,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };

    /// <summary>Lê a decisão que uma pessoa pode pedir, migrar ou recusar; nulo para qualquer outra.</summary>
    public static ResultadoDaMigracao? TentarLerDaPessoa(string? codigo) => codigo switch
    {
        Migrar => ResultadoDaMigracao.Migrar,
        Recusar => ResultadoDaMigracao.Recusar,
        _ => null,
    };
}
