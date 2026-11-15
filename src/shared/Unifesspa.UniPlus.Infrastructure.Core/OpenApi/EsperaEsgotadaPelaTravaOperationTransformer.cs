namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no contrato a recusa por espera esgotada pela trava (<c>uniplus.concorrencia.espera_esgotada</c>), nas mutações
/// marcadas com <see cref="EmiteEsperaEsgotadaPelaTravaAttribute"/>: o 503 sem prazo de nova tentativa, que manda reler o
/// estado e enviar de novo, com uma nova chave onde a rota exige a <c>Idempotency-Key</c>.
/// </summary>
/// <remarks>
/// A recusa nasce fora da action, então o autor da action não a declara; marcar a rota, e não repetir a declaração em cada
/// action, mantém o contrato preso ao mecanismo que a causa. Leitura não trava a linha e fica de fora.
/// </remarks>
public sealed class EsperaEsgotadaPelaTravaOperationTransformer : IOpenApiOperationTransformer
{
    private const string DescricaoComNovaChave =
        "Outra operação retém o processo além do limite de espera (uniplus.concorrencia.espera_esgotada). "
        + "Nada foi gravado, mas a recusa fica guardada pela Idempotency-Key: releia o estado do processo e envie com uma nova chave.";

    private const string DescricaoSemChave =
        "Outra operação retém o processo além do limite de espera (uniplus.concorrencia.espera_esgotada). "
        + "Nada foi gravado: releia o estado do processo e envie de novo.";

    private const string Status = "503";

    private static readonly string[] MetodosDeLeitura = ["GET", "HEAD", "OPTIONS"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (DeclaraAEspera(context))
        {
            operation.Responses ??= [];
            DeclararRecusa(operation.Responses, DeclaracaoDaEsperaEsgotada.ExigeChaveDeIdempotencia(context.Description.ActionDescriptor));
        }

        return Task.CompletedTask;
    }

    private static bool DeclaraAEspera(OpenApiOperationTransformerContext context) =>
        !MetodosDeLeitura.Contains(context.Description.HttpMethod ?? string.Empty, StringComparer.OrdinalIgnoreCase)
        && DeclaracaoDaEsperaEsgotada.Da(context.Description.ActionDescriptor) is not null;

    // Não sobrescreve o 503 que a action já declarou: ela pode ter motivo próprio para o status.
    private static void DeclararRecusa(OpenApiResponses respostas, bool exigeChave) =>
        respostas.TryAdd(Status, RecusaPorEsperaEsgotada(exigeChave));

    private static OpenApiResponse RecusaPorEsperaEsgotada(bool exigeChave) => new()
    {
        Description = exigeChave ? DescricaoComNovaChave : DescricaoSemChave,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["application/problem+json"] = new OpenApiMediaType { Schema = new OpenApiSchemaReference("ProblemDetails") },
        },
    };
}
