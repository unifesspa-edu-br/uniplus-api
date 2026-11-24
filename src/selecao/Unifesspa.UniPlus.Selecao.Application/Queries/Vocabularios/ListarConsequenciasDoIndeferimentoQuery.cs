namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Consulta o vocabulário das consequências do indeferimento de um documento exigido.</summary>
public sealed record ListarConsequenciasDoIndeferimentoQuery : IQuery<IReadOnlyList<CodigoComRotulo<ConsequenciaDoIndeferimento>>>;
