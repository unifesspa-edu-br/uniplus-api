namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Cria uma categoria de documento: código (chave natural, formato fechado
/// UPPER_SNAKE), nome (rótulo legível), descrição opcional e ordem de exibição no
/// catálogo. O ator de auditoria (<c>created_by</c>) é carimbado server-side via
/// <c>IUserContext</c>, não no payload.
/// </summary>
/// <remarks>
/// <para><c>Codigo</c> e <c>Nome</c> são <c>string?</c>, não <c>string</c>
/// (ADR-0125): sem validator FluentValidation garantindo não-nulo a montante,
/// o model binding automático do <c>[ApiController]</c> interceptaria um campo
/// ausente/nulo com um 400 genérico do ASP.NET, antes de o domínio rodar.</para>
/// <para><c>Ordem</c> é <c>int?</c> pela mesma razão: um <c>int</c> não-anulável
/// recebendo <c>null</c> explícito no JSON falha no desserializador antes do
/// domínio. Ausente equivale a zero.</para>
/// </remarks>
public sealed record CriarCategoriaDocumentoCommand(
    string? Codigo,
    string? Nome,
    string? Descricao = null,
    int? Ordem = null) : ICommand<Result<Guid>>;
