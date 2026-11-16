namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o solicitante declara ao pedir: por que precisa do acesso, até quando sugere que ele valha e se vai anexar
/// documento. O agregado a guarda, já conferida e normalizada, como o pedido a recebeu.
/// </summary>
/// <param name="Justificativa">Por que o acesso é necessário.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugere ao gestor; nulo, sem sugestão.</param>
/// <param name="AnexaraDocumento">
/// O solicitante vai anexar documento: o pedido espera a verificação dele antes do aceite e do gestor. No perfil de
/// governança é sempre verdadeiro, porque o documento de designação é obrigatório.
/// </param>
public sealed record DeclaracaoDoPedido(string Justificativa, DateTimeOffset? ValidaAteSugerida = null, bool AnexaraDocumento = false)
{
    public const int JustificativaMinLength = 10;
    public const int JustificativaMaxLength = 2000;

    // A justificativa já normalizada; o perfil de governança anexa sempre o documento de designação.
    internal DeclaracaoDoPedido Consolidada(string justificativa, Perfil perfil) => this with
    {
        Justificativa = justificativa,
        ValidaAteSugerida = ValidaAteSugerida?.ToUniversalTime(),
        AnexaraDocumento = AnexaraDocumento || perfil.Governanca is not null,
    };

    // No pedido protegido, o documento entra só se declarado na criação e só enquanto o pedido o aguarda; o pedido comum
    // continua aceitando anexo até a primeira decisão.
    internal DomainError? RecusaDoDocumento(TermoFixado? termo, SituacaoDoPedido situacao) =>
        termo is null ? null : RecusaDoNaoDeclarado() ?? RecusaDoJaVerificado(situacao);

    private DomainError? RecusaDoNaoDeclarado() =>
        AnexaraDocumento
            ? null
            : new DomainError(
                PedidoErrorCodes.AnexoNaoDeclarado,
                "O documento só se anexa ao pedido que declarou, na criação, que o anexaria. Cancele e refaça o pedido declarando o documento.");

    private static DomainError? RecusaDoJaVerificado(SituacaoDoPedido situacao) =>
        situacao == SituacaoDoPedido.AguardandoDocumento
            ? null
            : new DomainError(PedidoErrorCodes.SituacaoInvalida, "O documento declarado já foi verificado.");
}
