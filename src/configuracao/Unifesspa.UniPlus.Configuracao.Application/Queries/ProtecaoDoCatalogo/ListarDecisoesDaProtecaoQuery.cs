namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As decisões que a proteção de dados registrou (aprovação, ajuste recomendado e reprovação), paginadas por cursor
/// keyset ordenado, das mais recentes para as mais antigas (ADR-0026 + ADR-0094). A permissão de aprovar é
/// verificada na borda.
/// </summary>
/// <param name="Atos">Tokens canônicos das decisões pedidas; vazio, as três.</param>
/// <param name="Cadastro">Token canônico do cadastro do item.</param>
/// <param name="De">Data inicial (inclusive).</param>
/// <param name="Ate">Data final (inclusive).</param>
/// <param name="Busca">Trecho do nome ou do código do item, sem distinguir maiúsculas nem acentos.</param>
/// <param name="Minhas">Restringe às decisões de quem consulta.</param>
public sealed record ListarDecisoesDaProtecaoQuery(
    IReadOnlyList<string> Atos,
    string? Cadastro,
    DateOnly? De,
    DateOnly? Ate,
    string? Busca,
    bool Minhas,
    string? AfterSortKey,
    Guid? AfterId,
    int Limit,
    PaginationDirection Direction) : IQuery<Result<ListarDecisoesDaProtecaoResult>>;

/// <summary>Uma página das decisões e as âncoras (chave de ordenação e <c>Id</c>) das páginas vizinhas.</summary>
public sealed record ListarDecisoesDaProtecaoResult(
    IReadOnlyList<DecisaoDaProtecaoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
