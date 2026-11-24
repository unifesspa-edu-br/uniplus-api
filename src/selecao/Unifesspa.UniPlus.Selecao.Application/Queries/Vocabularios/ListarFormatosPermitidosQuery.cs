namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Consulta o vocabulário dos formatos de arquivo permitidos para a comprovação.</summary>
public sealed record ListarFormatosPermitidosQuery : IQuery<IReadOnlyList<CodigoComRotulo<FormatoPermitido>>>;
