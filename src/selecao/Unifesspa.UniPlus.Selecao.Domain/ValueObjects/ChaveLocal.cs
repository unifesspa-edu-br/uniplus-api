namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;

public readonly partial record struct ChaveLocal
{
    public const int ComprimentoMaximo = 64;
    public const string CodigoDeErro = "ChaveLocal.Invalida";

    public string Valor { get; }

    private ChaveLocal(string valor) => Valor = valor;

    public static Result<ChaveLocal> Criar(string? valor)
    {
        if (valor is null || valor.Length > ComprimentoMaximo || !Formato().IsMatch(valor))
        {
            return Result<ChaveLocal>.Failure(new DomainError(CodigoDeErro,
                "A chave deve começar por letra minúscula e conter apenas letras minúsculas, números e sublinhados entre palavras, com até 64 caracteres."));
        }

        return Result<ChaveLocal>.Success(new ChaveLocal(valor));
    }

    [GeneratedRegex(@"\A[a-z][a-z0-9]*(?:_[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();

    public override string ToString() => Valor ?? string.Empty;
}
