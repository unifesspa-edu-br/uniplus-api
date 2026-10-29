namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Payload do <c>PUT /api/autorizacao/perfis/{id}</c>: substitui nome, descrição e permissões.</summary>
public sealed record AtualizarPerfilCommand(
    Guid Id,
    string? Nome,
    string? Descricao,
    IReadOnlyList<string>? Permissoes) : ICommand<Result>;
