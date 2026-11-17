namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;

using System.Net;

/// <summary>A recusa registrada, de <c>Infrastructure.Core</c>, da atualização cujo Id da rota difere do Id do corpo.</summary>
public static class RecusaDoIdDivergente
{
    public static Task ConferirAsync(HttpResponseMessage resposta) =>
        ProblemaRegistrado.ConferirAsync(
            resposta,
            HttpStatusCode.BadRequest,
            "uniplus.contract.id_path_body_divergente",
            "Id da rota diferente do Id do corpo",
            "O Id informado na rota é diferente do Id do corpo da requisição; os dois precisam identificar o mesmo recurso.");
}
