namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirLocalidadeCommand"/> (UNI-REQ-0111): monta o value object
/// a partir do que foi declarado e delega ao domínio. A recusa por trio não declarado sai
/// aqui com o código que o catálogo público publica para a causa, em vez de virar erro
/// genérico de validação de contrato — é ele que leva o consumidor à explicação.
/// </summary>
public static class DefinirLocalidadeCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirLocalidadeCommand command,
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

        if (command.LocalidadeNaoDeclarada)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.LocalidadeAusente",
                "A localidade que rege a contagem dos prazos é obrigatória para alterar o processo seletivo."));
        }

        Result<LocalidadeRegente> localidadeResult = LocalidadeRegente.Criar(
            command.CodigoIbge, command.Nome, command.Uf);
        if (localidadeResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(localidadeResult.Error!);
        }

        Result definirResult = processo.DefinirLocalidade(localidadeResult.Value!);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
