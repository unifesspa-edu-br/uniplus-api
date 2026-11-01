namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os tipos de documento e fatos do catálogo que ainda não têm a proteção aprovada (ADR-0153), dos dois
/// cadastros, ordenados por cadastro e código. O volume é o do catálogo, portanto a leitura não é paginada.
/// </summary>
/// <param name="Acesso">O que quem consulta pode manter ou aprovar, decidido na borda.</param>
/// <param name="Situacao">Token canônico opcional (<c>PENDENTE</c>, <c>SOLICITADA</c> ou <c>NEGADA</c>) que restringe a lista.</param>
public sealed record ListarProtecaoDoCatalogoQuery(AcessoAoCatalogoDeProtecao Acesso, string? Situacao = null) : IQuery<Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>>>;
