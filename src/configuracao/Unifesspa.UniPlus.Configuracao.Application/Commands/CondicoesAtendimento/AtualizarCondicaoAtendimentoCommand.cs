namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma condição de atendimento especializado existente. O <c>Codigo</c> é
/// editável (com nova checagem de unicidade entre vivas), exceto quando o código
/// atual é o reservado <c>PCD</c> — que não pode ser renomeado. O <c>Id</c> é
/// imutável. O ator (<c>updated_by</c>) é carimbado server-side via
/// <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarCondicaoAtendimentoCommand(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao = null) : ICommand<Result>;
