namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based da criação de motivo (UNI-REQ-0120): valida o
/// payload por inteiro antes de qualquer I/O — código, descrição, fundamento e
/// resultado acumulam no mesmo lote (ADR-0125) — e só então confere a unicidade
/// do código, já normalizado.
/// </summary>
public static class CriarMotivoDecisaoIsencaoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarMotivoDecisaoIsencaoCommand command,
        IMotivoDecisaoIsencaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<MotivoDecisaoIsencao> criar = MotivoDecisaoIsencao.Criar(
            command.Codigo,
            command.Descricao,
            FundamentoIsencaoCodigo.FromCodigo(command.Fundamento),
            ResultadoPermitidoCodigo.FromCodigo(command.ResultadoPermitido));

        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        MotivoDecisaoIsencao motivo = criar.Value!;

        if (await repository.CodigoExisteAsync(motivo.Codigo.Valor, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(motivo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MotivoDecisaoIsencaoUniqueConstraintViolation.EhConflitoDeCodigo(ex))
        {
            // Sem descartar, a entidade Added continua rastreada e o
            // SaveChangesAsync que o Wolverine dispara depois do handler tenta a
            // mesma inserção fora deste catch — a violação estoura sem tradução
            // e o 409 pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(motivo.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(MotivoDecisaoIsencaoErrorCodes.CodigoJaExiste,
            "Já existe um motivo de decisão de isenção com o código informado.");
}
