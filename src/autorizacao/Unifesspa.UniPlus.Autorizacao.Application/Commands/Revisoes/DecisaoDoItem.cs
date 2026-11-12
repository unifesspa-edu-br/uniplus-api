namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;

/// <summary>
/// O item pronto para decidir: a revisão e a concessão travadas, quem decide com a condição do beneficiário lida sob a
/// trava do papel de Representante e o instante lido depois da última trava.
/// </summary>
internal sealed record DecisaoDoItem(
    RevisaoDeAcessos Revisao, ItemDaRevisao Item, Concessao Concessao, DecisorDoItem Decisor, DateTimeOffset Agora);
