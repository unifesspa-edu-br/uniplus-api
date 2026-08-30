namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Atualiza uma categoria de documento existente. O <c>Codigo</c> é editável (com
/// nova checagem de unicidade entre vivas) e o <c>Id</c> é imutável. O ator
/// (<c>updated_by</c>) é carimbado server-side via <c>IUserContext</c>.
/// </summary>
/// <remarks>
/// <c>Codigo</c>, <c>Nome</c> e <c>Ordem</c> são anuláveis (ADR-0125): sem
/// validator FluentValidation garantindo não-nulo a montante, o model binding
/// automático do <c>[ApiController]</c> interceptaria um campo ausente/nulo com um
/// 400 genérico do ASP.NET, antes de o domínio rodar.
/// </remarks>
public sealed record AtualizarCategoriaDocumentoCommand(
    Guid Id,
    string? Codigo,
    string? Nome,
    string? Descricao = null,
    int? Ordem = null) : ICommand<Result>;
