namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lista as revisões das concessões abertas e dentro do prazo em que quem lê tem concessão a decidir na etapa:
/// <c>gestao_de_acessos</c> ou <c>protecao_de_dados</c>.
/// </summary>
public sealed record ListarFilaDeRevisoesDasConcessoesQuery(string? Etapa) : IQuery<Result<IReadOnlyList<ItemDaListaDeRevisoesDto>>>;
