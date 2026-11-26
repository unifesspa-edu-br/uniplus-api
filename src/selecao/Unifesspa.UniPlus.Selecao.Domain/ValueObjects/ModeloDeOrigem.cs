namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O modelo de formulário de que o formulário do processo partiu: a identidade, o código e o nome que o
/// modelo tinha quando foi copiado. A cópia é a que vale; o modelo só registra de onde ela veio.
/// </summary>
public sealed record ModeloDeOrigem(Guid Id, string Codigo, string Nome)
{
    public const int CodigoMaxLength = 60;
    public const int NomeMaxLength = 200;

    /// <summary>O nome sem espaços nas pontas, como o cadastro o guarda.</summary>
    public string Nome { get; init; } = Nome.Trim();
}
