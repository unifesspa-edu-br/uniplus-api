namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarFaseCanonicaCommand"/> (convention-based Wolverine).
/// Constrói o agregado por inteiro e resolve a unidade sugerida no cadastro de unidades
/// (<see cref="IUnidadeReader"/>, ADR-0056), acumulando as recusas dos dois no mesmo lote
/// (422, ADR-0125): a unidade informada é congelada por snapshot-copy (ADR-0061). Só então
/// confere a unicidade do código entre vivos (409), persiste e confirma. Protege a corrida
/// check-then-act traduzindo a violação do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarFaseCanonicaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IUnidadeReader unidadeReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<FaseCanonica> criar = ConstruirFase(command);
        Result<UnidadeSugerida?> unidade = await UnidadeSugeridaResolver
            .ResolverAsync(command.UnidadeSugeridaId, unidadeReader, cancellationToken)
            .ConfigureAwait(false);
        if (criar.IsFailure || unidade.IsFailure)
        {
            return Result<Guid>.ValidationFailure([.. criar.Errors, .. unidade.Errors]);
        }
        criar.Value!.DefinirUnidadeSugerida(unidade.Value);
        return await GravarNovaAsync(criar.Value, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result<FaseCanonica> ConstruirFase(CriarFaseCanonicaCommand command) =>
        FaseCanonica.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.BaseLegal,
            command.OrigemData,
            command.Sinalizadores());

    private static async Task<Result<Guid>> GravarNovaAsync(
        FaseCanonica fase,
        IFaseCanonicaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteEntreVivosAsync(fase.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(fase, cancellationToken).ConfigureAwait(false);

        return await PerdeuACorridaDoCodigoAsync(unitOfWork, cancellationToken).ConfigureAwait(false)
            ? Result<Guid>.Failure(CodigoJaExisteErro())
            : Result<Guid>.Success(fase.Id);
    }

    private static async Task<bool> PerdeuACorridaDoCodigoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (Exception ex) when (UniqueConstraintViolation.EhConflito(ex, RestricoesDoBanco.FaseCanonicaCodigoVivo))
        {
            // Sem descartar, a entidade Added continua rastreada e qualquer SaveChanges
            // posterior no mesmo escopo repete a inserção fora deste catch — a mesma
            // violação estoura sem tradução, e o 409 pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return true;
        }
    }

    private static DomainError CodigoJaExisteErro() =>
        new(FaseCanonicaErrorCodes.CodigoJaExiste,
            "Já existe uma fase canônica viva com o código informado.");
}
