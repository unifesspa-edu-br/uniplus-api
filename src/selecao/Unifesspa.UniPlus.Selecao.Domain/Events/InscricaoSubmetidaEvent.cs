namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Emitido pela primeira <see cref="Entities.Inscricao.Submeter"/> de uma inscrição (issue #1858).
/// Só ids, número e instante — nunca dado pessoal (§4.6 do plano da #1855): um consumidor que
/// precisar de respostas ou documento tem de reconsultar o agregado, nunca ler o evento.
/// </summary>
public sealed record InscricaoSubmetidaEvent(
    Guid InscricaoId,
    Guid ProcessoSeletivoId,
    string NumeroInscricao,
    DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
