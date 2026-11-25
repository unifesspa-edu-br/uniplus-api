namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Validação de um campo obrigatório de código de cadastro, sem alterar o valor recebido.</summary>
public sealed record CampoDeCodigo
{
    public CampoDeCodigo(string nome, int maximo, string erroObrigatorio, string erroFormato)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximo, FormatoDoCodigo.ComprimentoMaximo);
        Nome = nome;
        Maximo = maximo;
        ErroObrigatorio = erroObrigatorio;
        ErroFormato = erroFormato;
    }

    public string Nome { get; }
    public int Maximo { get; }
    public string ErroObrigatorio { get; }
    public string ErroFormato { get; }

    public FieldError? Recusa(string? valor, string caminho)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return new(caminho, new DomainError(ErroObrigatorio, $"{Nome} é obrigatório."));
        }

        return valor.Length > Maximo || !FormatoDoCodigo.TemFormatoValido(valor)
            ? new(caminho, new DomainError(ErroFormato,
                $"{Nome} deve começar com letra maiúscula e ter até {Maximo} caracteres: letras maiúsculas sem acento, algarismos e sublinhado entre palavras."))
            : null;
    }
}
