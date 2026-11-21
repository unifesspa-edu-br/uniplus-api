namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Lê o vocabulário fechado dos estados de resolução de um fato do candidato. Não há estado a consultar: o conjunto é governado por código e muda por versão da API,
/// não por cadastro.
/// </summary>
public sealed record ListarEstadosFatoQuery : IQuery<IReadOnlyList<CodigoComRotulo<EstadoFato>>>;
