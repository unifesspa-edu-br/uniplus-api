namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um tipo de deficiência existente. O <c>Nome</c> é editável, com nova
/// checagem de unicidade entre vivos; a descrição também pode ser editada. O
/// <c>Id</c> é imutável. O ator (<c>updated_by</c>) é carimbado server-side via
/// <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarTipoDeficienciaCommand(
    Guid Id,
    string Nome,
    string? Descricao = null) : ICommand<Result>;
