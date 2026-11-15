namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

/// <summary>
/// As mutações da rota (ou de todas as rotas do controller) podem esperar por uma trava de linha retida por outra operação
/// e, esgotado o limite, responder 503. O contrato precisa declarar o status, ou o cliente gerado trata a resposta como
/// inesperada.
/// </summary>
/// <remarks>
/// O atributo da ação vence o do controller. A resposta é sempre uma recusa guardada pela chave de idempotência, quando a
/// rota a exige: repetir a requisição depois de perder a corrida pode agir sobre algo que o vencedor mudou, então o
/// cliente relê o estado e envia de novo.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class EmiteEsperaEsgotadaPelaTravaAttribute : Attribute
{
}
