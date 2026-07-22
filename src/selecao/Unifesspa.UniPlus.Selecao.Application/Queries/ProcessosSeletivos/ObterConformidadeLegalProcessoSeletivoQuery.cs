namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Consulta a conformidade legal do Processo Seletivo (Story #853, CA-16)
/// contra o catálogo <c>ObrigatoriedadeLegal</c> vigente na
/// <paramref name="DataReferencia"/> informada — mesma fonte que o gate de
/// congelamento usa, nunca uma segunda leitura em paralelo.
/// </summary>
/// <param name="ProcessoSeletivoId">Processo avaliado.</param>
/// <param name="DataReferencia">
/// Data de corte (Story #852 §3.1). Explícita e obrigatória — um processo em
/// rascunho não tem <c>DadosEdital.PeriodoInscricaoInicio</c> persistido
/// ainda, e o avaliador nunca lê o relógio (ADR-0068).
/// </param>
public sealed record ObterConformidadeLegalProcessoSeletivoQuery(
    Guid ProcessoSeletivoId,
    DateOnly DataReferencia) : IQuery<ConformidadeLegalProcessoSeletivoDto?>;
