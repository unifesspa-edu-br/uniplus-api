namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de banca (UNI-REQ-0064): código (chave natural canônica imutável),
/// nome, fase típica opcional (rótulo orientativo, não vinculante) e descrição
/// opcional. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarTipoBancaCommand(
    string Codigo,
    string? Nome = null,
    string? FaseTipica = null,
    string? Descricao = null) : ICommand<Result<Guid>>;
