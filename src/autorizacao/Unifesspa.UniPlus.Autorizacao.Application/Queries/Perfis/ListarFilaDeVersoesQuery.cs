namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista as versões pendentes que aguardam a etapa: <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>. A pendente
/// vencida não consta.
/// </summary>
public sealed record ListarFilaDeVersoesQuery(string? Fila) : IQuery<Result<IReadOnlyList<ItemDaFilaDeVersoesDto>>>;
