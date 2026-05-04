namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Lista editais paginados por cursor (ADR-0026). Os parâmetros já chegam
/// decodificados — o controller é responsável por decifrar o cursor opaco e
/// validar o <c>limit</c> antes de despachar a query, mantendo
/// <c>Application</c> independente de <c>Infrastructure.Core</c>.
/// </summary>
/// <param name="AfterId">Identificador do último item da página anterior; <c>null</c> retorna a primeira janela.</param>
/// <param name="Limit">Tamanho máximo da página a retornar.</param>
public sealed record ListarEditaisQuery(Guid? AfterId, int Limit) : IQuery<ListarEditaisResult>;
