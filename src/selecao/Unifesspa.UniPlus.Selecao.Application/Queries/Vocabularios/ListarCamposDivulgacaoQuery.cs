namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Lê o vocabulário fechado de campos publicáveis na divulgação de resultado
/// (UNI-REQ-0050), com as duas propriedades que decidem o que a tela permite: o piso que não
/// se remove e o campo que obriga justificativa.
/// </summary>
public sealed record ListarCamposDivulgacaoQuery : IQuery<IReadOnlyList<CampoDivulgacaoDto>>;
