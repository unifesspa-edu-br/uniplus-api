namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma fase canônica (UNI-REQ-0064): código (chave natural canônica imutável),
/// nome, descrição opcional, dono típico como token canônico UPPER_SNAKE
/// (<c>DonoTipico</c>), e os sinalizadores <c>AgrupaEtapas</c> /
/// <c>PermiteComplementacao</c> (falsos por omissão). O ator de auditoria
/// (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarFaseCanonicaCommand(
    string Codigo,
    string? Nome = null,
    string? Descricao = null,
    string? DonoTipico = null,
    bool AgrupaEtapas = false,
    bool PermiteComplementacao = false,
    string? BaseLegal = null) : ICommand<Result<Guid>>;
