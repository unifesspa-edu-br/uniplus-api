namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Inclui num lugar do processo, uma fase ou uma etapa, um motivo do cadastro (UNI-REQ-0285). Depois da publicação, só
/// sob a sessão de retificação e com <paramref name="Justificativa"/>, de uso interno (UNI-REQ-0124).
/// </summary>
public sealed record IncluirMotivoNoProcessoCommand(
    Guid ProcessoSeletivoId,
    LugarDaDecisao Lugar,
    Guid MotivoDeDecisaoId,
    string? Justificativa,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
