namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Promove o rascunho revisado a uma nova versão imutável. Recusa rascunho não
/// revisado, sem texto ou sem base legal (regras do agregado). O ator é
/// resolvido server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record PromoverVersaoTermoConsentimentoCommand(Guid Id) : ICommand<Result>;
