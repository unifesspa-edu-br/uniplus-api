namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Marca o rascunho corrente como revisado — portão distinto da edição comum
/// (<see cref="EditarRascunhoTermoConsentimentoCommand"/>). O ator é resolvido
/// server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record MarcarRevisadoTermoConsentimentoCommand(Guid Id) : ICommand<Result>;
