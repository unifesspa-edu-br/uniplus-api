namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Exclui o rascunho que ainda não virou processo, na revisão que a pessoa leu.</summary>
/// <param name="Id">O identificador do rascunho.</param>
/// <param name="Precondicao">O <c>If-Match</c> decodificado, obrigatório.</param>
public sealed record ExcluirRascunhoDeProcessoSeletivoCommand(Guid Id, PrecondicaoIfMatch Precondicao) : ICommand<Result>;
