namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterTipoDocumentoPorIdQuery(Guid Id) : IQuery<Result<TipoDocumentoDto>>;
