namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Devolve o rascunho da publicação do operador corrente, ou <see langword="null"/> quando não
/// há nenhum — inclusive quando o que havia já venceu.
/// </summary>
public sealed record ObterRascunhoDaPublicacaoQuery(Guid ProcessoSeletivoId) : IQuery<RascunhoDaPublicacaoDto?>;
