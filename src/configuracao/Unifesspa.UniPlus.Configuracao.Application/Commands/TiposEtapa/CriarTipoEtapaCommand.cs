namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// <c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c> (ADR-0125):
/// sem validator FluentValidation garantindo não-nulo a montante, o model
/// binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.
/// A <c>Avaliacao</c> traz os sinalizadores de caráter e os conceitos admitidos, anuláveis pelo mesmo motivo
/// (ver <see cref="AvaliacaoDasEtapasInput"/>).
/// </remarks>
public sealed record CriarTipoEtapaCommand(
    string? Codigo,
    string? Nome,
    AvaliacaoDasEtapasInput? Avaliacao,
    string? Descricao = null) : ICommand<Result<Guid>>;
