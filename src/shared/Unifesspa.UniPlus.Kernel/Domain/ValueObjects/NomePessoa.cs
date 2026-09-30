namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Text;
using System.Text.RegularExpressions;

using Results;

/// <summary>
/// Nome de pessoa: ao menos nome e sobrenome, só com letras (acentuadas inclusive), espaço,
/// apóstrofo e hífen. O texto é normalizado antes da conferência: forma composta (NFC), apóstrofo
/// tipográfico como o do teclado e espaços repetidos como um só. A máscara expõe a inicial de
/// cada parte.
/// </summary>
public sealed partial record NomePessoa
{
    private const int TamanhoMaximo = 200;

    public string Valor { get; }

    private NomePessoa(string valor) => Valor = valor;

    public static Result<NomePessoa> Criar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<NomePessoa>.Failure(new DomainError("NomePessoa.Vazio", "Nome é obrigatório."));

        string normalizado = EspacosRepetidos()
            .Replace(nome.Trim().Normalize(NormalizationForm.FormC), " ")
            .Replace('\u2019', '\'')
            .Replace('\u02BC', '\'');
        if (normalizado.Length > TamanhoMaximo || !NomeValido().IsMatch(normalizado))
            return Result<NomePessoa>.Failure(new DomainError(
                "NomePessoa.Invalido", "Nome inválido: informe nome e sobrenome, só com letras."));

        return Result<NomePessoa>.Success(new NomePessoa(normalizado));
    }

    /// <summary>A inicial de cada parte do nome, como em <c>M*** d* S***</c>.</summary>
    public string Mascarado =>
        string.Join(' ', Valor.Split(' ').Select(static parte => $"{parte[0]}{new string('*', Math.Min(parte.Length - 1, 3))}"));

    public override string ToString() => Mascarado;

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRepetidos();

    [GeneratedRegex(@"^\p{L}+(?:['\-]\p{L}+)*(?: \p{L}+(?:['\-]\p{L}+)*)+$")]
    private static partial Regex NomeValido();
}
