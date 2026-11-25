namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarTipoAtoPublicadoCommand"/> (convention-based
/// Wolverine): valida o payload por inteiro primeiro (sem I/O — validação sempre
/// vence I/O), só então confere que nenhuma versão viva do mesmo código intercepta
/// a janela informada, cria o agregado (que não pode falhar de novo, já validado),
/// persiste e commita.
/// </summary>
/// <remarks>
/// A consulta prévia dá a mensagem legível no caso comum; a exclusion constraint
/// fecha a corrida check-then-act. São papéis distintos, não duplicação: entre a
/// consulta e o <c>SaveChanges</c> cabe outra transação, e só o banco a vê.
/// </remarks>
public static class CriarTipoAtoPublicadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<CamposDoTipoAtoPublicado> validacao = TipoAtoPublicado.ValidarDeclaracao(command.Declaracao());
        if (validacao.IsFailure)
        {
            return Result<Guid>.ValidationFailure(validacao.Errors);
        }

        return await Criar(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> Criar(
        CriarTipoAtoPublicadoCommand command, ITipoAtoPublicadoRepository repository,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
            command.Codigo!, command.VigenciaInicio, command.VigenciaFim, null, cancellationToken)
            .ConfigureAwait(false);
        if (sobreposta)
        {
            return Result<Guid>.Failure(VigenciaSobrepostaErro());
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(command.Declaracao()).Value!;
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        return await Salvar(unitOfWork, tipo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> Salvar(
        IUnitOfWork unitOfWork, TipoAtoPublicado tipo, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExclusionConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && ExclusionConstraintViolation.IsVigenciaConflict(constraint))
        {
            return DescartarAlteracoesComConflito(unitOfWork);
        }

        return Result<Guid>.Success(tipo.Id);
    }

    private static Result<Guid> DescartarAlteracoesComConflito(IUnitOfWork unitOfWork)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result<Guid>.Failure(VigenciaSobrepostaErro());
    }

    internal static DomainError VigenciaSobrepostaErro() =>
        new(TipoAtoPublicadoErrorCodes.VigenciaSobreposta,
            $"Já existe uma versão viva do tipo de ato vigente em parte do período informado.");
}
