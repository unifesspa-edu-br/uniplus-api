namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using Errors;

using Microsoft.AspNetCore.Http;

using OpenApi;

/// <summary>
/// A resposta 503 da espera esgotada pela trava (<c>uniplus.concorrencia.espera_esgotada</c>), escrita pelo middleware de
/// erros e pelo filtro de idempotência, que a guarda na chave. A causa e o recurso travado não vão à resposta.
/// </summary>
/// <remarks>
/// Não traz prazo de nova tentativa: repetir a mesma requisição depois da corrida perdida pode agir sobre o que o vencedor
/// mudou, então a resposta manda reler o estado e enviar de novo, com uma nova chave onde a rota exige
/// <c>Idempotency-Key</c>, porque a mesma chave devolve esta recusa.
/// </remarks>
internal static class RespostaDaEsperaEsgotadaPelaTrava
{
    private const string DetalheComNovaChave =
        "Outra operação sobre este processo está em andamento. Releia o estado do processo e envie novamente com uma nova chave de idempotência.";

    private const string DetalheSemChave =
        "Outra operação sobre este processo está em andamento. Releia o estado do processo e envie novamente.";

    internal static Task EscreverAsync(HttpContext context, IProblemTypeUriFactory problemTypeUriFactory) =>
        ProblemaNaResposta.EscreverAsync(
            context,
            problemTypeUriFactory,
            ErrosDaFronteiraHttp.EsperaPelaTravaEsgotada,
            DeclaracaoDaEsperaEsgotada.ExigeChaveDeIdempotencia(context) ? DetalheComNovaChave : DetalheSemChave);
}
