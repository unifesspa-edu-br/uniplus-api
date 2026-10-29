namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// Handler do <see cref="CriarFaseCanonicaCommand"/> (convention-based Wolverine).
/// Orquestra: construção do agregado por inteiro primeiro (sem I/O — formato,
/// pertença ao conjunto canônico e coerência acumulados no mesmo lote, 422), só
/// então a unicidade do código entre vivos (409), persistência e commit. Protege
/// a corrida check-then-act traduzindo a violação do índice único parcial em
/// <c>CodigoJaExiste</c>. A unidade sugerida, quando informada, é resolvida por
/// <see cref="IUnidadeReader"/> (ADR-0056) e congelada por snapshot-copy (ADR-0061).
/// </summary>
public static class CriarFaseCanonicaCommandHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c> pelo mesmo motivo de <c>CriarOfertaCursoCommandHandler</c>: o
    /// handler injeta o reader de Organização e o unit of work de Configuração, e o detector de
    /// transação do Wolverine falha por ambiguidade de <c>DbContext</c>.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<Guid>> Handle(
        CriarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IUnidadeReader unidadeReader,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<FaseCanonica> criar = FaseCanonica.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.AgrupaEtapas,
            command.PermiteComplementacao,
            command.BaseLegal,
            command.ColetaInscricao,
            command.ColetaSolicitacaoIsencao,
            command.OrigemData);

        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        FaseCanonica fase = criar.Value!;

        Result<UnidadeSugerida?> unidade = await UnidadeSugeridaResolver
            .ResolverAsync(command.UnidadeSugeridaId, unidadeReader, cancellationToken)
            .ConfigureAwait(false);
        if (unidade.IsFailure)
        {
            return Result<Guid>.ValidationFailure(unidade.Errors);
        }

        fase.DefinirUnidadeSugerida(unidade.Value);

        if (await repository.CodigoExisteEntreVivosAsync(fase.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(fase, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Sem descartar, a entidade Added continua rastreada e qualquer SaveChanges
            // posterior no mesmo escopo repete a inserção fora deste catch — a mesma
            // violação estoura sem tradução, e o 409 pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(fase.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            "Já existe uma fase canônica viva com o código informado.");
}
