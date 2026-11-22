namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Consulta os encerramentos da votação com código e rótulo.</summary>
public sealed record ListarEncerramentosDaVotacaoQuery : IQuery<IReadOnlyList<CodigoComRotulo<EncerramentoDaVotacao>>>;
