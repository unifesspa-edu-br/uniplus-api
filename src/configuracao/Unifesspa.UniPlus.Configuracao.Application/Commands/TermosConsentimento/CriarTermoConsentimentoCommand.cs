namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um termo de consentimento/declaração no catálogo (UNI-REQ-0086/RN-COL-05).
/// Nasce sempre <c>EM_ELABORACAO</c>, com rascunho vazio ou com campos iniciais —
/// marcar revisado e promover são operações explícitas subsequentes.
/// </summary>
/// <remarks>
/// <c>Nome</c> é <c>string?</c>, não <c>string</c> (ADR-0125): sem valor default,
/// para o schema OpenAPI continuar listando-o como obrigatório; nulo, para o
/// campo ausente escapar do <c>[ApiController]</c> e chegar à validação de
/// domínio.
/// </remarks>
public sealed record CriarTermoConsentimentoCommand(
    string? Nome,
    string? TextoRascunho,
    string? BaseLegalRascunho,
    string? FormaAceiteRascunho) : ICommand<Result<Guid>>;
