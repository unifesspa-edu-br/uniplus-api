namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O motivo que acompanha uma decisão, recusa, revogação, cancelamento ou remoção: texto aparado, sem o caractere nulo e
/// de até <see cref="MaximoDeCaracteres"/> caracteres, contados como a coluna os conta, em caracteres e não em unidades de
/// texto UTF-16 (o caractere fora do plano básico, como um emoji, vale um).
/// </summary>
public sealed record MotivoInformado
{
    public const int MaximoDeCaracteres = 500;

    private MotivoInformado(string valor) => Valor = valor;

    public string Valor { get; }

    /// <summary>
    /// O motivo aparado, ou nulo quando está em branco, passa do limite ou traz o caractere nulo, que não é espaço em branco e
    /// cabe no tamanho, mas a coluna textual do banco não armazena: sem esta recusa o valor só falharia ao gravar, e o payload
    /// inválido responderia 500 em vez da validação de domínio. Quem recebe nulo escolhe a recusa do seu contexto.
    /// </summary>
    public static MotivoInformado? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string aparado = texto.Trim();

        return aparado.Contains('\0', StringComparison.Ordinal) || aparado.EnumerateRunes().Count() > MaximoDeCaracteres
            ? null
            : new MotivoInformado(aparado);
    }

    public override string ToString() => Valor;
}
