namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Monta as etapas com as recusas acumuladas por etapa (ADR-0125) e entrega ao processo, que confere
/// a estrutura do formulário, a fase e os itens já definidos.
/// </summary>
public static class DefinirFormularioCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        // A precondição de concorrência precede a validação do payload (ADR-0110 D9).
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<FieldError> erros = [];
        List<EtapaFormulario> etapas = [];
        IReadOnlyList<EtapaFormularioInput> entradas = command.Etapas ?? [];
        for (int i = 0; i < entradas.Count; i++)
        {
            if (entradas[i] is not { } entrada)
            {
                erros.Add(new($"etapas[{i}]", new DomainError(FormularioProcessoErrorCodes.EtapaCodigoInvalido, "A etapa veio nula.")));
                continue;
            }

            Result<EtapaFormulario> etapa = EtapaFormulario.Criar(
                entrada.Codigo, entrada.Ordem, EstruturaFormulario.TipoDoToken(entrada.Tipo), EstruturaFormulario.BlocoDoToken(entrada.Bloco),
                entrada.Titulo, entrada.Descricao, entrada.Aviso);
            if (etapa.IsSuccess)
            {
                etapas.Add(etapa.Value!);
            }
            else
            {
                erros.AddRange(etapa.Errors.Select(e => new FieldError($"etapas[{i}].{e.Field}", e.Error)));
            }
        }

        if (erros.Count > 0)
        {
            // As recusas do formulário somam-se às das etapas (ADR-0125).
            erros.AddRange(processo.ConferirFormulario(command.Finalidade, command.FaseId, command.Titulo, etapas, etapasCompletas: false));
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        Result definir = processo.DefinirFormulario(command.Finalidade, command.FaseId, command.Titulo, etapas, command.Precondicao);
        if (definir.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definir.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }

    internal static DomainError ProcessoNaoEncontrado(Guid id) =>
        new("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {id} não encontrado.");
}

/// <summary>Remove o formulário da finalidade; a regra de só em rascunho é do processo.</summary>
public static class RemoverFormularioCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        RemoverFormularioCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        Result remover = processo.RemoverFormulario(command.Finalidade, command.Precondicao);
        if (remover.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(remover.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
