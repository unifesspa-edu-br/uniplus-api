namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Query paginada por cursor (ADR-0026) com filtros admin. <c>Vigentes</c>
/// default <see langword="true"/> — leitura pública padrão; admin pode
/// passar <see langword="false"/> para incluir regras com
/// <c>VigenciaFim</c> passada.
/// </summary>
public sealed record ListarObrigatoriedadesLegaisQuery(
    Guid? AfterId,
    int Take,
    string? TipoEditalCodigo,
    CategoriaObrigatoriedade? Categoria,
    string? Proprietario,
    bool Vigentes) : IQuery<ListarObrigatoriedadesLegaisResult>;
