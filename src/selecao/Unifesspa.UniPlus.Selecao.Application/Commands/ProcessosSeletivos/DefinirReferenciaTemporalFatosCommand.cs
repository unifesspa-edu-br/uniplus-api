namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define (ou remove) a política que ancora <c>FAIXA_ETARIA</c> na publicação (Story
/// #554, PR #896 — B-03 do plano). Passar <see langword="null"/> em <see cref="Tipo"/>
/// remove a referência — a ausência é estado válido enquanto nenhuma exigência tem
/// gatilho por idade (só vira pendência de publicação nesse caso).
/// </summary>
public sealed record DefinirReferenciaTemporalFatosCommand(
    Guid ProcessoSeletivoId,
    string? Tipo,
    DateOnly? Data,
    Guid? FaseId,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
