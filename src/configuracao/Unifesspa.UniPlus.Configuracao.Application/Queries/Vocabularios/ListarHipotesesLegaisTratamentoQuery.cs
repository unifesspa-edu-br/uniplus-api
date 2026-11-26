namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Consulta os códigos e rótulos de HipoteseLegalTratamento.</summary>
public sealed record ListarHipotesesLegaisTratamentoQuery : IQuery<IReadOnlyList<CodigoComRotulo<HipoteseLegalTratamento>>>;
