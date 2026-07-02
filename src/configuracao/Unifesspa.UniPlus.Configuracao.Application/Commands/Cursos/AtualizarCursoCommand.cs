namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza um curso existente. O <c>Codigo</c> é editável (mesmo expediente do
/// TipoDocumento), com nova checagem de unicidade entre vivos; os demais
/// atributos (nome, grau, nível de ensino, grupo de área do ENEM) também podem
/// ser editados. O <c>Id</c> é imutável. O ator (<c>updated_by</c>) é carimbado
/// server-side via <c>IUserContext</c>.
/// </summary>
public sealed record AtualizarCursoCommand(
    Guid Id,
    string Codigo,
    string Nome,
    string Grau,
    string NivelEnsino,
    string? GrupoAreaEnem = null) : ICommand<Result>;
