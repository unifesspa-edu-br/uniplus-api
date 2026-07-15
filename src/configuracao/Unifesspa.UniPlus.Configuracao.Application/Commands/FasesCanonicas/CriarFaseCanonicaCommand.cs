namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma fase canônica (UNI-REQ-0064): código (chave natural canônica imutável),
/// nome, descrição opcional, dono típico como token canônico UPPER_SNAKE
/// (<c>DonoTipico</c>), origem da data como token canônico UPPER_SNAKE
/// (<c>OrigemData</c>), e os sinalizadores <c>AgrupaEtapas</c> /
/// <c>PermiteComplementacao</c> / <c>ProduzResultado</c> / <c>ResultadoDefinitivo</c> /
/// <c>ColetaInscricao</c> (falsos por omissão — <c>ResultadoDefinitivo</c> verdadeiro
/// exige <c>ProduzResultado</c> verdadeiro, CA-04). O ator de auditoria
/// (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarFaseCanonicaCommand(
    string Codigo,
    string? Nome = null,
    string? Descricao = null,
    string? DonoTipico = null,
    bool AgrupaEtapas = false,
    bool PermiteComplementacao = false,
    string? BaseLegal = null,
    bool ProduzResultado = false,
    bool ResultadoDefinitivo = false,
    bool ColetaInscricao = false,
    string? OrigemData = null) : ICommand<Result<Guid>>;
