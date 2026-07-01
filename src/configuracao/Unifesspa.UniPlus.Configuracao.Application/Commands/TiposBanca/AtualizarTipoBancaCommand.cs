namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um tipo de banca existente. O <c>Codigo</c> e o <c>Id</c> são
/// <b>imutáveis</b>: o comando <b>não</b> aceita código — o handler carrega a
/// entidade e a atualiza sem alterá-lo. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarTipoBancaCommand(
    Guid Id,
    string? Nome = null,
    string? FaseTipica = null,
    string? Descricao = null) : ICommand<Result>;
