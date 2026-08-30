namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterCategoriaDocumentoPorIdQuery(Guid Id) : IQuery<CategoriaDocumentoDto?>;
