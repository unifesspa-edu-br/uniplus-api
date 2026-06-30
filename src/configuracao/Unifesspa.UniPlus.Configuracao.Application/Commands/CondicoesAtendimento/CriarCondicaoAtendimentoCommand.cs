namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma condição de atendimento especializado: código (chave natural, formato
/// fechado UPPER_SNAKE), nome (rótulo legível) e descrição opcional. O ator de
/// auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
public sealed record CriarCondicaoAtendimentoCommand(
    string Codigo,
    string Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
