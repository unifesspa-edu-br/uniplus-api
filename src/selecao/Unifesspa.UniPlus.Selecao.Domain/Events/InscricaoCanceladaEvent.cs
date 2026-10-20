namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Emitido por <see cref="Entities.Inscricao.Cancelar"/> quando o candidato desfaz a inscrição enviada.
/// Só ids, número e instante, no mesmo molde de <see cref="InscricaoSubmetidaEvent"/>: nem motivo nem
/// ator saem no evento.
/// </summary>
public sealed record InscricaoCanceladaEvent(
    Guid InscricaoId,
    Guid ProcessoSeletivoId,
    string NumeroInscricao,
    DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
