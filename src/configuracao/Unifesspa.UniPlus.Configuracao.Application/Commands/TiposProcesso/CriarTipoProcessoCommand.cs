namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125):
/// sem validator FluentValidation garantindo não-nulo a montante, o model
/// binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoProcessoCommand(string? Codigo, string? Nome, string? Descricao = null) : ICommand<Result<Guid>>;
