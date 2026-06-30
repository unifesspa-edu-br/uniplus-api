namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um tipo de deficiência: nome (chave natural, único entre vivos) e
/// descrição opcional. O ator de auditoria (<c>created_by</c>) é carimbado
/// server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarTipoDeficienciaCommand(
    string Nome,
    string? Descricao = null) : ICommand<Result<Guid>>;
