namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de banca (UNI-REQ-0064): código (chave natural canônica imutável),
/// nome, fase típica opcional (rótulo orientativo, não vinculante) e descrição
/// opcional. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <c>Codigo</c> é <c>string?</c>, não <c>string</c> (ADR-0125): sem validator
/// FluentValidation garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400 genérico
/// do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarTipoBancaCommand(
    string? Codigo,
    string? Nome = null,
    string? FaseTipica = null,
    string? Descricao = null) : ICommand<Result<Guid>>;
