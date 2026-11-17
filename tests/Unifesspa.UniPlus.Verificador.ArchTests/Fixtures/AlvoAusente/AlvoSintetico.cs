namespace Microsoft.EntityFrameworkCore.Sintetico;

/// <summary>Assembly sintético de nome proibido, que o diretório de saída do teste nunca recebe.</summary>
public static class AlvoSintetico
{
    // static readonly e não const: o compilador inlina const e a referência some do manifesto de quem a usa.
    public static readonly string Nome = "alvo ausente";
}
