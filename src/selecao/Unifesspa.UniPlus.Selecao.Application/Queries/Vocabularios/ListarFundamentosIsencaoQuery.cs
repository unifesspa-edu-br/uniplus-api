namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lê o vocabulário fechado de fundamentos de isenção (UNI-REQ-0101). Não há estado a
/// consultar: o conjunto é governado por código e muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarFundamentosIsencaoQuery : IQuery<IReadOnlyList<FundamentoIsencaoDto>>;
