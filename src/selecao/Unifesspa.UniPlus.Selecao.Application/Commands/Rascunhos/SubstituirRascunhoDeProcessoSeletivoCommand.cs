namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Substitui o documento inteiro do rascunho pela revisão que a pessoa leu, identificada pelo <c>If-Match</c>.</summary>
/// <param name="Id">O identificador do rascunho.</param>
/// <param name="Documento">O texto JSON do documento que passa a valer; o bloco omitido é apagado.</param>
/// <param name="Precondicao">O <c>If-Match</c> decodificado, obrigatório.</param>
public sealed record SubstituirRascunhoDeProcessoSeletivoCommand(Guid Id, string Documento, PrecondicaoIfMatch Precondicao)
    : ICommand<Result<RascunhoSalvoDto>>;
