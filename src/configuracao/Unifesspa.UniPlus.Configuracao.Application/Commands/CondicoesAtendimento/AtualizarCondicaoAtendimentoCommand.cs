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
/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record AtualizarCondicaoAtendimentoCommand(
    Guid Id,
    string? Codigo,
    string? Nome,
    string? Descricao = null) : ICommand<Result>;
