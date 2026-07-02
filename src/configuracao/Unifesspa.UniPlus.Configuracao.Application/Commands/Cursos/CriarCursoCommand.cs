namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Cursos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria um curso — matriz curricular pura: código (chave natural), nome, grau e
/// nível de ensino (texto livre obrigatório) e grupo de área do ENEM opcional
/// (domínio fechado de quatro grupos, Res. 805/2024). Código e-MEC, local e
/// unidade pertencem à <c>OfertaCurso</c> (#749), não aqui. O ator de auditoria
/// (<c>created_by</c>) é carimbado server-side via <c>IUserContext</c>, não no payload.
/// </summary>
public sealed record CriarCursoCommand(
    string Codigo,
    string Nome,
    string Grau,
    string NivelEnsino,
    string? GrupoAreaEnem = null) : ICommand<Result<Guid>>;
