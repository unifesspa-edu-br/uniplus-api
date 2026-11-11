namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Handler convention-based da criação de motivo: valida o payload por inteiro antes de qualquer
/// I/O (ADR-0125) e só então confere a unicidade do código dentro do tipo, já normalizado.
/// </summary>
public static class CriarMotivoDeDecisaoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarMotivoDeDecisaoCommand command,
        IMotivoDeDecisaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<MotivoDeDecisao> criar = MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisaoCodigo.FromCodigo(command.Tipo),
            command.Codigo,
            command.Descricao,
            command.Subclassificacao,
            SentidoDoMotivoCodigo.FromCodigo(command.Sentido)));
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        return await GravarAsync(criar.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> GravarAsync(
        MotivoDeDecisao motivo, IMotivoDeDecisaoRepository repository, ISelecaoUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(motivo.Tipo, motivo.Codigo.Valor, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(MotivoDeDecisaoHandlerErros.CodigoJaExiste());
        }

        await repository.AdicionarAsync(motivo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MotivoDeDecisaoUniqueConstraintViolation.EhConflitoDeCodigo(ex))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync que o
            // Wolverine dispara depois do handler repete a inserção fora deste catch: 500 em vez de 409.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(MotivoDeDecisaoHandlerErros.CodigoJaExiste());
        }

        return Result<Guid>.Success(motivo.Id);
    }
}
