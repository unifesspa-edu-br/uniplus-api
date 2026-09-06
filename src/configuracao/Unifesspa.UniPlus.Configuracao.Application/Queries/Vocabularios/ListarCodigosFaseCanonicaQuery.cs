namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Lê o vocabulário fechado de fases canônicas (UNI-REQ-0139). Não há estado a consultar: o
/// conjunto é governado por código e muda por versão da API, não por cadastro — embora as
/// dezesseis fases também nasçam semeadas por migration (<c>FaseCanonicaSeed</c>), o
/// vocabulário aqui é o código fechado, não o registro administrável.
/// </summary>
public sealed record ListarCodigosFaseCanonicaQuery : IQuery<IReadOnlyList<FaseCanonicaVocabularioDto>>;
