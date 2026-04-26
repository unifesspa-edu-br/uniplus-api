namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Consulta um edital pelo <see cref="Id"/>. O contrato usa
/// <c>EditalDto?</c> (não <c>Result&lt;T&gt;</c>) porque "não encontrado" é
/// estado normal de leitura — o <see cref="EditalController"/> mapeia
/// <c>null</c> diretamente para 404 sem precisar carregar
/// <see cref="DomainError"/> no canal de query.
/// </summary>
public sealed record ObterEditalQuery(Guid Id) : IQuery<EditalDto?>;
