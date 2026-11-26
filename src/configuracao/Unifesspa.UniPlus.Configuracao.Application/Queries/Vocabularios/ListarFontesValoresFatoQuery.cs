namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta os códigos e rótulos de FonteValoresFato.</summary>
public sealed record ListarFontesValoresFatoQuery : IQuery<IReadOnlyList<CodigoComRotulo<FonteValoresFato>>>;
