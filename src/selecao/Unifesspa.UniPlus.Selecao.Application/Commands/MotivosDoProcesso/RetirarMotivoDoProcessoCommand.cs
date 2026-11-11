namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDoProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Retira do processo um motivo escolhido que ainda não consta do edital publicado.</summary>
public sealed record RetirarMotivoDoProcessoCommand(
    Guid ProcessoSeletivoId,
    Guid MotivoDeDecisaoId,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
