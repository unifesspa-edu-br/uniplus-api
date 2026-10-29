namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Payload do <c>POST /api/autorizacao/perfis</c>.</summary>
public sealed record CriarPerfilCommand(
    string? Nome,
    string? Descricao,
    IReadOnlyList<string>? Permissoes) : ICommand<Result<Guid>>;
