namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta as unidades do prazo de emissão.</summary>
public sealed record ListarUnidadesDoPrazoDeEmissaoQuery : IQuery<IReadOnlyList<CodigoComRotulo<UnidadeDoPrazoDeEmissao>>>;
