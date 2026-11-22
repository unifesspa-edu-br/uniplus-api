namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Consulta as formas de declarar uma regra de comprovação.</summary>
public sealed record ListarFormasDeComprovacaoQuery : IQuery<IReadOnlyList<CodigoComRotulo<FormaDeComprovacao>>>;
