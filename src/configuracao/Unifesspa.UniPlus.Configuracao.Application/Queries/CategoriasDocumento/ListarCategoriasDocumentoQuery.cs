namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Lista o catálogo de categorias de documento vivas, na ordem de exibição.
/// Sem parâmetros de paginação: o catálogo é conjunto de referência fechado e de
/// baixo volume, consumido inteiro por carregamento de tela — mesmo contrato do
/// catálogo de fatos do candidato.
/// </summary>
public sealed record ListarCategoriasDocumentoQuery : IQuery<ListarCategoriasDocumentoResult>;
