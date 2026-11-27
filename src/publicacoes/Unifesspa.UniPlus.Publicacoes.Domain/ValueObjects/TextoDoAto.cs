namespace Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// A regra dos textos que o ato declara: sem espaços nas pontas e até o limite. O obrigatório vazio é
/// recusado; o opcional vazio vira ausente.
/// </summary>
internal static class TextoDoAto
{
    internal static ParteConferida<string> Obrigatorio(string? valor, int maximo, string campo, string rotulo)
    {
        string? aparado = valor?.Trim();
        return string.IsNullOrEmpty(aparado) || aparado.Length > maximo
            ? Recusado(campo, $"{rotulo} é obrigatório e deve ter no máximo {maximo} caracteres.")
            : ParteConferida<string>.Aceita(aparado);
    }

    internal static ParteConferida<string> Opcional(string? valor, int maximo, string campo, string rotulo)
    {
        string? aparado = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        return aparado?.Length > maximo
            ? Recusado(campo, $"{rotulo} deve ter no máximo {maximo} caracteres.")
            : ParteConferida<string>.Aceita(aparado);
    }

    private static ParteConferida<string> Recusado(string campo, string mensagem) =>
        ParteConferida<string>.Recusada([new(campo, new DomainError(AtoNormativoErrorCodes.TextoInvalido, mensagem))]);
}
