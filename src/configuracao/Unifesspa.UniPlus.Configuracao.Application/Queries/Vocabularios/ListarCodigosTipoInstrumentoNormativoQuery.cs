namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Lê o vocabulário fechado de tipos de instrumento normativo. Não há estado
/// a consultar: o conjunto é governado por código e muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarCodigosTipoInstrumentoNormativoQuery : IQuery<IReadOnlyList<TipoInstrumentoNormativoVocabularioDto>>;
