namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma aresta de precedência entre duas fases canônicas (UNI-REQ-0064):
/// código da antecessora, código da sucessora e se as janelas podem se sobrepor
/// (falso por omissão — a não-sobreposição é a regra padrão). O ator de auditoria
/// (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// Os dois códigos são <c>string?</c>, não <c>string</c> (ADR-0125): sem validator
/// FluentValidation garantindo não-nulo a montante, o model binding automático do
/// <c>[ApiController]</c> interceptaria um campo ausente/nulo com um 400 genérico
/// do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record CriarPrecedenciaFaseCommand(
    string? AntecessoraCodigo,
    string? SucessoraCodigo,
    bool PermiteSobreposicao = false) : ICommand<Result<Guid>>;
