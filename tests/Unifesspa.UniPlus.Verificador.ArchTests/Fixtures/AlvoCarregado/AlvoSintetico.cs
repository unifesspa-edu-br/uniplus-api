namespace Npgsql.Sintetico;

/// <summary>Assembly sintético de nome proibido, presente no diretório de saída do teste.</summary>
public static class AlvoSintetico
{
    // static readonly e não const: o compilador inlina const e a referência some do manifesto de quem a usa.
    public static readonly string Nome = "alvo carregado";
}
