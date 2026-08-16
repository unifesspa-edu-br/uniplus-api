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
/// <remarks>
/// <c>Codigo</c>, <c>Nome</c>, <c>Grau</c> e <c>NivelEnsino</c> são <c>string?</c>,
/// não <c>string</c> (ADR-0125): sem validator FluentValidation garantindo
/// não-nulo a montante, o model binding automático do <c>[ApiController]</c>
/// interceptaria um campo ausente/nulo com um 400 genérico do ASP.NET, antes de
/// o domínio rodar.
/// </remarks>
public sealed record AtualizarCursoCommand(
    Guid Id,
    string? Codigo,
    string? Nome,
    string? Grau,
    string? NivelEnsino,
    string? GrupoAreaEnem = null) : ICommand<Result>;
