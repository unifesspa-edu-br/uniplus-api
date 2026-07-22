namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Altera o motivo da sessão editorial em curso (Story #860, ADR-0110 D5). Como toda
/// mutação sob sessão, exige a precondição e devolve o <c>ETag</c> novo.
/// </summary>
public static class AlterarMotivoRetificacaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        AlterarMotivoRetificacaoCommand command,
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
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Aqui o If-Match é INCONDICIONAL, ao contrário dos seis Definir* — onde a
        // obrigatoriedade depende de haver sessão aberta (D5). Esta rota só existe PARA a
        // sessão: chamá-la sem precondição é falha de protocolo, e ela é respondida ANTES
        // da checagem de existência do rascunho (D9, precedência "3 antes de 10"). Dizer
        // 409 primeiro mandaria o cliente caçar um rascunho que talvez exista, quando o
        // defeito está no que ele mesmo deixou de enviar.
        if (!command.Precondicao.Presente)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "Precondicao.Requerida",
                "Esta rota edita a sessão editorial em curso — informe o If-Match com o ETag dela."));
        }

        Result alterado = processo.AlterarMotivoRetificacao(command.Motivo, command.Precondicao);
        if (alterado.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(alterado.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
