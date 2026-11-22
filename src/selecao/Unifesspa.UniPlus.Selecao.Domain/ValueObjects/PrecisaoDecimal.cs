namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Limites dos valores decimais declarados na configuração.</summary>
public static class PrecisaoDecimal
{
    public const string Fracao = "A fração do nível deve ter no máximo 4 casas decimais e ficar entre 0 e 1.";
    public const string Peso = "Peso da etapa deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string NotaMinima = "Nota mínima deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string NotaMaxima = "Nota máxima deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string Passo = "Passo do lançamento deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string Pontuacao = "A pontuação máxima do item deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string Prazo = "O prazo de interposição deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";
    public const string PrazoSegunda = "O prazo da 2ª instância deve ter no máximo 14 dígitos inteiros e 4 casas decimais.";

    public static bool Cabe(decimal? valor) => valor is null
        || (decimal.GetBits(valor.Value)[3] >> 16 & 0xFF) <= 4 && valor.Value is > -100000000000000m and < 100000000000000m;

    public static FieldError? Recusa(decimal? valor, string campo, string mensagem) =>
        Cabe(valor) ? null : new(campo, new DomainError(CodigosDeValidacao.Formato, mensagem));
}
