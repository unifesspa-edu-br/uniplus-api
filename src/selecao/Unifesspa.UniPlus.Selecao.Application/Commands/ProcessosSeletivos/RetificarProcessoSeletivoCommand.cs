namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Kernel.Results;

/// <summary>
/// Retifica um processo já publicado (RN08, Story #759, T5 #786, ADR-0101):
/// emite um novo Edital de natureza retificação vinculado ao Edital vigente,
/// com motivo obrigatório, e congela um novo <c>SnapshotPublicacao</c> — o
/// snapshot anterior permanece imutável, tudo na mesma transação. O ator
/// (<c>IUserContext.UserId</c>) nunca é input do command — vem do contexto
/// autenticado.
/// </summary>
public sealed record RetificarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    Guid EditalRetificadoId,
    string Motivo,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId) : ICommand<Result>;
