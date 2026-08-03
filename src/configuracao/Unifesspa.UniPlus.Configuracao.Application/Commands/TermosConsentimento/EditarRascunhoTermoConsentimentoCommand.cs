namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Edita o rascunho corrente de um termo de consentimento (texto, base legal,
/// forma de aceite). Operação do papel de aplicação comum — se o rascunho já
/// estava <c>REVISADO</c>, a edição é aceita mas devolve o status a
/// <c>EM_ELABORACAO</c> e limpa a marca de revisão (regra do agregado).
/// </summary>
public sealed record EditarRascunhoTermoConsentimentoCommand(
    Guid Id,
    string? TextoRascunho,
    string? BaseLegalRascunho,
    string? FormaAceiteRascunho) : ICommand<Result>;
