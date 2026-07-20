namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoNo"/> e o código textual canônico do wire — mesma
/// convenção de <see cref="ChaveDistincaoCodigo"/>/<see cref="TipoEntidadeCodigo"/>, mas os
/// tokens NÃO são UPPER_SNAKE do nome do membro: <c>FOLHA</c>/<c>E</c>/<c>OU</c> são os
/// tokens já estabelecidos por <c>NoExigenciaInput</c>/<c>NoExigenciaDto</c> (comando de
/// escrita e DTO de leitura, Story #920) — este mapeamento só os centraliza para que o
/// envelope canônico (Story #923) use exatamente os mesmos, sem uma terceira grafia.
/// </summary>
public static class TipoNoCodigo
{
    public const string Folha = "FOLHA";
    public const string GrupoE = "E";
    public const string GrupoOu = "OU";

    public static string ToCodigo(this TipoNo tipo) => tipo switch
    {
        TipoNo.Folha => Folha,
        TipoNo.GrupoE => GrupoE,
        TipoNo.GrupoOu => GrupoOu,
        TipoNo.Nenhum => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoNo.Nenhum é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoNo desconhecido."),
    };

    public static TipoNo FromCodigo(string? codigo) => codigo switch
    {
        Folha => TipoNo.Folha,
        GrupoE => TipoNo.GrupoE,
        GrupoOu => TipoNo.GrupoOu,
        _ => TipoNo.Nenhum,
    };
}
