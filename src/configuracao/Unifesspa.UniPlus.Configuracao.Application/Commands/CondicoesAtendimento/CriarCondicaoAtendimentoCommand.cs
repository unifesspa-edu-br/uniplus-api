namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma condição de atendimento especializado: código (chave natural, formato
/// fechado UPPER_SNAKE), nome (rótulo legível) e descrição opcional. O ator de
/// auditoria (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>,
/// não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarCondicaoAtendimentoCommand(
    string? Codigo,
    string? Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
