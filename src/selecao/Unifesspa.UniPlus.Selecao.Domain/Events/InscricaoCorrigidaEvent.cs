namespace Unifesspa.UniPlus.Selecao.Domain.Events;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Emitido por uma <see cref="Entities.Inscricao.Submeter"/> que corrige uma inscrição já
/// submetida (issue #1858) — a correção preserva o número e gera comprovante novo, sem tocar no
/// anterior. Só ids, número e instante, no mesmo molde de <see cref="InscricaoSubmetidaEvent"/>.
/// </summary>
public sealed record InscricaoCorrigidaEvent(
    Guid InscricaoId,
    Guid ProcessoSeletivoId,
    string NumeroInscricao,
    DateTimeOffset OccurredOn) : DomainEventBase(OccurredOn);
