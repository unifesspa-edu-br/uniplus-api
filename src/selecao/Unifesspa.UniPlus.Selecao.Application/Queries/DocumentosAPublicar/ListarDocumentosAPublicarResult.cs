namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado do <see cref="ListarDocumentosAPublicarQuery"/> — todos os documentos
/// do processo, do mais recente para o mais antigo. Sem paginação: o conjunto é
/// o histórico de envios de um único processo, e cortá-lo em páginas esconderia
/// justamente o documento antigo que o editor precisa reconhecer.
/// </summary>
public sealed record ListarDocumentosAPublicarResult(
    IReadOnlyList<DocumentoAPublicarDto> Items);
