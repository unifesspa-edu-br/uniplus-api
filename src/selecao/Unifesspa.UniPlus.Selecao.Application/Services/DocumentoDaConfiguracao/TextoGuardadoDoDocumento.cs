namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// O texto que o rascunho guarda: o documento normalizado, compacto e com os caracteres não ASCII como estão, sem expandi-los em
/// escapes <c>\uXXXX</c>. O texto vai para uma coluna <c>jsonb</c> e sai como <c>application/json</c>, nunca como HTML, por isso
/// o escape de marcas de HTML não protege nada. Os limites do documento valem de novo para este texto, a forma em que o
/// documento é guardado: nenhum documento guardado passa deles.
/// </summary>
public static class TextoGuardadoDoDocumento
{
    private static readonly JsonSerializerOptions Opcoes = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Result<string> Gerar(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        string texto = documento.ToJsonString(Opcoes);
        IReadOnlyList<FieldError> causas =
            [.. ConferenciaDaEstruturaDoDocumento.ConferirLimites(texto), .. ConferenciaDaEstruturaDoDocumento.ConferirQuantidadeDeNos(documento)];
        return causas.Count == 0
            ? Result<string>.Success(texto)
            : Result<string>.ValidationFailure(causas);
    }
}
