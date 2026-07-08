namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using DTOs;

/// <summary>
/// Resolve o snapshot congelado vigente do Processo Seletivo num instante
/// EXPLÍCITO (RN08, Story #759 T6 #787, ADR-0075/0076): o Edital vivo publicado
/// de MAIOR <c>data_publicacao</c> ≤ <see cref="Instante"/>. Quando o instante
/// é nulo, o handler o resolve do <c>TimeProvider</c> injetado e o repassa
/// adiante — o seletor nunca lê o relógio por trás do contrato (ADR-0068).
/// </summary>
public sealed record ObterSnapshotVigenteQuery(
    Guid ProcessoSeletivoId,
    DateTimeOffset? Instante) : IQuery<Result<SnapshotVigenteDto>>;
