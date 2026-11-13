namespace Unifesspa.UniPlus.Autorizacao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio do termo de responsabilidade, declarados uma vez para que o domínio, os handlers e o
/// registro de erros da API não divirjam.
/// </summary>
public static class TermoErrorCodes
{
    public const string SemVersaoVigente = "Termo.SemVersaoVigente";
    public const string VersaoNaoEncontrada = "Termo.VersaoNaoEncontrada";
    public const string TextoInvalido = "Termo.TextoInvalido";
    public const string AtoInvalido = "Termo.AtoInvalido";
    public const string TextoIgualAoVigente = "Termo.TextoIgualAoVigente";
    public const string VigenteAlterado = "Termo.VigenteAlterado";
    public const string AutorNaoIdentificado = "Termo.AutorNaoIdentificado";
    public const string ConflitoDeConcorrencia = "Termo.ConflitoDeConcorrencia";
}
