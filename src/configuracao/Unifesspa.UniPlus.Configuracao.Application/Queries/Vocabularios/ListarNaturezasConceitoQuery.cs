namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Lê o vocabulário fechado de naturezas de tipo de conceito. Não há estado a consultar: o conjunto é
/// governado por código e muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarNaturezasConceitoQuery : IQuery<IReadOnlyList<CodigoComRotulo<FormaDoConceito>>>;
