namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define (ou substitui) a oferta de atendimento especializado do processo
/// (CA-06 da Story #758). Cada dimensão referencia o cadastro vivo do módulo
/// Configuração por <c>Id</c> — os dados congelados (código, nome) são lidos
/// no handler via snapshot-copy (ADR-0061). Tipo de deficiência só é aceito
/// quando a condição PcD está entre as condições ofertadas (ADR-0067).
/// </summary>
public sealed record DefinirOfertaAtendimentoCommand(
    Guid ProcessoSeletivoId,
    IReadOnlyList<Guid> CondicaoIds,
    IReadOnlyList<Guid> RecursoIds,
    IReadOnlyList<Guid> TipoDeficienciaIds,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
