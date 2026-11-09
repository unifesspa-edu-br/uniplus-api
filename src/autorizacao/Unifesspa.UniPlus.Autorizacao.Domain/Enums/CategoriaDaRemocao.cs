namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>Por que as permissões foram removidas do perfil, quando quem remove quer dizer.</summary>
public enum CategoriaDaRemocao
{
    Incidente = 1,

    ErroDeComposicao = 2,

    MudancaDeFuncao = 3,
}

/// <summary>Código textual canônico de <see cref="CategoriaDaRemocao"/>, o valor gravado e exposto.</summary>
public static class CategoriaDaRemocaoCodigo
{
    public const string Incidente = "incidente";
    public const string ErroDeComposicao = "erro_de_composicao";
    public const string MudancaDeFuncao = "mudanca_de_funcao";

    public static string ToCodigo(this CategoriaDaRemocao categoria) => categoria switch
    {
        CategoriaDaRemocao.Incidente => Incidente,
        CategoriaDaRemocao.ErroDeComposicao => ErroDeComposicao,
        CategoriaDaRemocao.MudancaDeFuncao => MudancaDeFuncao,
        _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, null),
    };

    public static CategoriaDaRemocao FromCodigo(string codigo) => codigo switch
    {
        Incidente => CategoriaDaRemocao.Incidente,
        ErroDeComposicao => CategoriaDaRemocao.ErroDeComposicao,
        MudancaDeFuncao => CategoriaDaRemocao.MudancaDeFuncao,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };

    /// <summary>Lê o código informado; nulo quando não é de nenhuma categoria.</summary>
    public static CategoriaDaRemocao? TentarLer(string? codigo) => codigo switch
    {
        Incidente => CategoriaDaRemocao.Incidente,
        ErroDeComposicao => CategoriaDaRemocao.ErroDeComposicao,
        MudancaDeFuncao => CategoriaDaRemocao.MudancaDeFuncao,
        _ => null,
    };
}
