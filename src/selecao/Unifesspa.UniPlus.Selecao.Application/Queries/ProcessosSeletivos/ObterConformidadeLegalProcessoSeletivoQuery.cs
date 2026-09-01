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
/// Dia em que se pergunta quais obrigatoriedades estavam em vigor (Story #852 §3.1).
/// <para>
/// Opcional: quando ausente, o handler o deriva da janela da fase que coleta inscrição, no
/// fuso institucional — a MESMA derivação que o gate de publicação faz (issue #1350), o que
/// mantém de pé a garantia do CA-16/CA-17 de que a consulta responde pela data do gate.
/// Continua aceito explicitamente porque um processo em rascunho pode ainda não ter cronograma
/// que resolva a data, e o avaliador nunca lê o relógio (ADR-0068).
/// </para>
/// </param>
public sealed record ObterConformidadeLegalProcessoSeletivoQuery(
    Guid ProcessoSeletivoId,
    DateOnly? DataReferencia = null) : IQuery<ConformidadeLegalProcessoSeletivoDto?>;
