namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoEntidade"/> e o código textual canônico UPPER_SNAKE
/// do wire de comando — mesma convenção de <see cref="ChaveDistincaoCodigo"/>.
/// </summary>
public static class TipoEntidadeCodigo
{
    public const string MembroNucleoFamiliar = "MEMBRO_NUCLEO_FAMILIAR";
    public const string PessoaJuridicaVinculada = "PESSOA_JURIDICA_VINCULADA";

    public static string ToCodigo(this TipoEntidade tipo) => tipo switch
    {
        TipoEntidade.MembroNucleoFamiliar => MembroNucleoFamiliar,
        TipoEntidade.PessoaJuridicaVinculada => PessoaJuridicaVinculada,
        TipoEntidade.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoEntidade.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoEntidade desconhecido."),
    };

    public static TipoEntidade FromCodigo(string? codigo) => codigo switch
    {
        MembroNucleoFamiliar => TipoEntidade.MembroNucleoFamiliar,
        PessoaJuridicaVinculada => TipoEntidade.PessoaJuridicaVinculada,
        _ => TipoEntidade.Nenhuma,
    };
}
