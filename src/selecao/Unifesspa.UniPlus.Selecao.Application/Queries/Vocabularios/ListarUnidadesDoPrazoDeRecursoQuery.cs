namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lê as unidades em que o edital pode declarar o prazo do recurso, com o código e o rótulo de cada uma.
/// </summary>
public sealed record ListarUnidadesDoPrazoDeRecursoQuery : IQuery<IReadOnlyList<CodigoComRotulo<UnidadePrazo>>>;
