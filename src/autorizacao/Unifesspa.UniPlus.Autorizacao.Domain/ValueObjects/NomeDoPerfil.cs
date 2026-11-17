namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O nome de um perfil: obrigatório, sem espaços nas pontas, sem o caractere nulo e com o tamanho que a coluna comporta. A
/// contagem é em unidades de texto (UTF-16), a mesma com que o banco e as versões do perfil já o limitavam.
/// </summary>
public sealed record NomeDoPerfil
{
    public const int MinimoDeCaracteres = 3;
    public const int MaximoDeCaracteres = 120;

    private NomeDoPerfil(string valor) => Valor = valor;

    public string Valor { get; }

    /// <summary>O nome aparado, ou nulo quando está em branco, fora dos limites de tamanho ou traz o caractere nulo.</summary>
    public static NomeDoPerfil? Normalizar(string? texto) =>
        TextoLivre.Normalizar(texto, MinimoDeCaracteres, MaximoDeCaracteres) is { } normalizado ? new NomeDoPerfil(normalizado) : null;

    /// <summary>O nome que o cadastro já guarda: não se confere de novo, só se exige que exista.</summary>
    public static NomeDoPerfil Reidratar(string persistido)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persistido);

        return new NomeDoPerfil(persistido);
    }

    public override string ToString() => Valor;
}
