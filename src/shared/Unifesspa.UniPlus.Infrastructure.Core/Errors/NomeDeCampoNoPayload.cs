namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Text.Json;

/// <summary>
/// O caminho de um campo como o cliente o escreve no corpo JSON. O framework e o FluentValidation
/// nomeiam o campo pelo identificador C# (PascalCase); o que o frontend usa para achar o controle
/// do formulário, e o que o <c>errors[].field</c> da ADR-0023 publica, é o casing do payload.
/// </summary>
public static class NomeDeCampoNoPayload
{
    /// <summary>
    /// A conversão é por segmento e preserva o índice: <c>Quadro[0].Quantidade</c> vira
    /// <c>quadro[0].quantidade</c>, e <c>[0].Documento.Fato</c> vira <c>[0].documento.fato</c>.
    /// </summary>
    public static string De(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
        {
            return string.Empty;
        }

        return string.Join('.', caminho.Split('.').Select(static segmento =>
        {
            int indice = segmento.IndexOf('[', StringComparison.Ordinal);
            return indice < 0
                ? JsonNamingPolicy.CamelCase.ConvertName(segmento)
                : JsonNamingPolicy.CamelCase.ConvertName(segmento[..indice]) + segmento[indice..];
        }));
    }
}
