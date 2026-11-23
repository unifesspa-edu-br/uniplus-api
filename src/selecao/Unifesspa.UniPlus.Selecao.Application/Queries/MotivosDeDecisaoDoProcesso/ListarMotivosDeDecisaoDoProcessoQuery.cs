namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisaoDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Os motivos que o processo escolheu, por tipo e código, em cada fase ou etapa. Sem <paramref name="NumeroDaVersao"/>, a
/// escolha atual; com ela, a que valia naquela versão publicada do edital.
/// </summary>
public sealed record ListarMotivosDeDecisaoDoProcessoQuery(Guid ProcessoSeletivoId, int? NumeroDaVersao) : IQuery<Result<IReadOnlyList<MotivoDeDecisaoDoProcessoDto>>>;
