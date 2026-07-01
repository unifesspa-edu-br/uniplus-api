namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma fase canônica existente. O <c>Codigo</c> e o <c>Id</c> são
/// <b>imutáveis</b>: o comando <b>não</b> aceita código — o handler carrega a
/// entidade e a atualiza sem alterá-lo. O dono típico chega como token canônico
/// UPPER_SNAKE. O ator (<c>updated_by</c>) é carimbado server-side via
/// <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarFaseCanonicaCommand(
    Guid Id,
    string? Nome = null,
    string? Descricao = null,
    string? DonoTipico = null,
    bool AgrupaEtapas = false,
    bool PermiteComplementacao = false,
    string? BaseLegal = null) : ICommand<Result>;
