namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Situação de uma concessão dentro da janela de migração, desde que o lote da gestão foi enviado.</summary>
public enum SituacaoDaMigracao
{
    /// <summary>A concessão é do próprio gestor que enviou o lote: outro gestor a decide.</summary>
    Retida = 1,

    /// <summary>A gestão migrou, e a proteção de dados ainda decide.</summary>
    AguardandoProtecaoDeDados = 2,

    /// <summary>Final: a concessão passou para a versão-alvo.</summary>
    Migrada = 3,

    /// <summary>Final: a concessão foi recusada e revogada.</summary>
    Recusada = 4,

    /// <summary>Final: ninguém decidiu até o fim da janela, e a concessão foi revogada.</summary>
    NaoMigrada = 5,

    /// <summary>Final: a concessão saiu da lista antes da decisão, por revogação ou vencimento.</summary>
    ForaDaLista = 6,
}

/// <summary>Código textual canônico de <see cref="SituacaoDaMigracao"/>, o valor gravado e exposto.</summary>
public static class SituacaoDaMigracaoCodigo
{
    public const string Retida = "retida";
    public const string AguardandoProtecaoDeDados = "aguardando_protecao_de_dados";
    public const string Migrada = "migrada";
    public const string Recusada = "recusada";
    public const string NaoMigrada = "nao_migrada";
    public const string ForaDaLista = "fora_da_lista";

    public static string ToCodigo(this SituacaoDaMigracao situacao) => situacao switch
    {
        SituacaoDaMigracao.Retida => Retida,
        SituacaoDaMigracao.AguardandoProtecaoDeDados => AguardandoProtecaoDeDados,
        SituacaoDaMigracao.Migrada => Migrada,
        SituacaoDaMigracao.Recusada => Recusada,
        SituacaoDaMigracao.NaoMigrada => NaoMigrada,
        SituacaoDaMigracao.ForaDaLista => ForaDaLista,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    public static SituacaoDaMigracao FromCodigo(string codigo) => codigo switch
    {
        Retida => SituacaoDaMigracao.Retida,
        AguardandoProtecaoDeDados => SituacaoDaMigracao.AguardandoProtecaoDeDados,
        Migrada => SituacaoDaMigracao.Migrada,
        Recusada => SituacaoDaMigracao.Recusada,
        NaoMigrada => SituacaoDaMigracao.NaoMigrada,
        ForaDaLista => SituacaoDaMigracao.ForaDaLista,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };

    /// <summary>A situação que não muda mais.</summary>
    public static bool EhFinal(this SituacaoDaMigracao situacao) =>
        situacao is not (SituacaoDaMigracao.Retida or SituacaoDaMigracao.AguardandoProtecaoDeDados);
}
