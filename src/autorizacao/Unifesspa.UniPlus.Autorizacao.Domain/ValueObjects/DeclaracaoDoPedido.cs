namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que o solicitante declara ao pedir: por que precisa do acesso, até quando sugere que ele valha e se vai anexar
/// documento. O agregado confere cada item; a declaração só os carrega.
/// </summary>
/// <param name="Justificativa">Por que o acesso é necessário.</param>
/// <param name="ValidaAteSugerida">Fim de vigência que o solicitante sugere ao gestor; nulo, sem sugestão.</param>
/// <param name="AnexaraDocumento">
/// O solicitante vai anexar documento: o pedido espera a verificação dele antes do aceite e do gestor. No perfil de
/// governança é sempre verdadeiro, porque o documento de designação é obrigatório.
/// </param>
public sealed record DeclaracaoDoPedido(string? Justificativa, DateTimeOffset? ValidaAteSugerida = null, bool AnexaraDocumento = false);
