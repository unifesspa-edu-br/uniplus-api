namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A versão do termo que o cliente diz ter apresentado, conferida com a esperada na criação (a vigente) e no aceite (a
/// fixada): igual à esperada; ausente quando o pedido exige o termo; presente quando não o exige.
/// </summary>
internal static class ApresentacaoDoTermo
{
    private const string Campo = "termoVersaoApresentadaId";

    /// <param name="apresentada">A versão que o cliente apresentou; nula se não apresentou nenhuma.</param>
    /// <param name="esperada">A versão que o pedido espera; nula quando ele não exige o termo.</param>
    internal static Result? Conferir(Guid? apresentada, Guid? esperada) =>
        esperada is { } fixada ? ConferirExigida(apresentada, fixada) : ConferirNaoExigida(apresentada);

    private static Result? ConferirNaoExigida(Guid? apresentada) =>
        apresentada is null ? null : Invalida("Este pedido não exige o termo de responsabilidade, e a versão não deve ser informada.");

    private static Result? ConferirExigida(Guid? apresentada, Guid esperada)
    {
        if (apresentada is null)
        {
            return Invalida("Este pedido exige informar a versão do termo de responsabilidade apresentada.");
        }

        return apresentada == esperada
            ? null
            : Result.Failure(new DomainError(
                PedidoErrorCodes.TermoDivergente,
                "A versão do termo apresentada não é a vigente (na criação) ou a fixada no pedido (no aceite). Leia o termo novamente e repita a operação."));
    }

    private static Result Invalida(string mensagem) => Result.ValidationFailure(
        [new FieldError(Campo, new DomainError(PedidoErrorCodes.ApresentacaoDoTermoInvalida, mensagem))]);
}
