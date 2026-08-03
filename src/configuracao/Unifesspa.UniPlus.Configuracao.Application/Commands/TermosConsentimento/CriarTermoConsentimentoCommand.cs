namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um termo de consentimento/declaração no catálogo (UNI-REQ-0086/RN-COL-05).
/// Nasce sempre <c>EM_ELABORACAO</c>, com rascunho vazio ou com campos iniciais —
/// marcar revisado e promover são operações explícitas subsequentes.
/// </summary>
public sealed record CriarTermoConsentimentoCommand(
    string Nome,
    string? TextoRascunho,
    string? BaseLegalRascunho,
    string? FormaAceiteRascunho) : ICommand<Result<Guid>>;
