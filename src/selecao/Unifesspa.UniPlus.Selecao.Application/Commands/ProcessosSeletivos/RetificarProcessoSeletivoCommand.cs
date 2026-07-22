namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Retifica um processo já publicado (RN08, Story #759, T5 #786, ADR-0101):
/// emite um novo Edital de natureza retificação sucedendo o Edital vigente,
/// com motivo obrigatório, e sucede a <c>VersaoConfiguracao</c> corrente — a
/// versão anterior permanece imutável, tudo na mesma transação. O ator
/// (<c>IUserContext.UserId</c>) nunca é input do command — vem do contexto
/// autenticado. O Edital sucedido é o vigente do próprio agregado, resolvido
/// no servidor: como <c>Edital</c> é entidade interna do agregado
/// <c>ProcessoSeletivo</c> e a cadeia é linear, o cliente endereça apenas a
/// raiz (o <c>ProcessoSeletivoId</c>), nunca um id de Edital interno.
/// </summary>
public sealed record RetificarProcessoSeletivoCommand(
    Guid ProcessoSeletivoId,
    string Motivo,
    string? Numero,
    DateOnly PeriodoInscricaoInicio,
    DateOnly PeriodoInscricaoFim,
    Guid DocumentoEditalId,
    DadosDoAto Ato) : ICommand<Result>;
