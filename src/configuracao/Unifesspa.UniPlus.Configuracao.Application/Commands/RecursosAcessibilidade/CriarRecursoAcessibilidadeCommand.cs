namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um recurso de acessibilidade: nome (chave natural) e descrição opcional.
/// O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Nome</c> é <c>string?</c>, não <c>string</c> (ADR-0125): sem valor default,
/// para o schema OpenAPI continuar listando-o como obrigatório; nulo, para o
/// campo ausente escapar do <c>[ApiController]</c> e chegar à validação de
/// domínio.
/// </remarks>
public sealed record CriarRecursoAcessibilidadeCommand(
    string? Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
