namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os tipos de documento e fatos do catálogo que ainda não têm a proteção aprovada (ADR-0153), dos dois
/// cadastros, ordenados por cadastro e código. O volume é o do catálogo, portanto a leitura não é paginada.
/// </summary>
/// <param name="Situacao">Token canônico opcional (<c>PENDENTE</c>, <c>SOLICITADA</c>, <c>AJUSTE_RECOMENDADO</c> ou <c>REPROVADA</c>) que restringe a lista.</param>
public sealed record ListarProtecaoDoCatalogoQuery(string? Situacao = null) : IQuery<Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>>;
