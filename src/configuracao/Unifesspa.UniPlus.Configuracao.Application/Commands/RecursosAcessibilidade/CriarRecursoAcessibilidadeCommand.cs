namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um recurso de acessibilidade: nome (chave natural) e descrição opcional.
/// O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarRecursoAcessibilidadeCommand(
    string Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
