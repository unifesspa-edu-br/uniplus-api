namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarTipoAtoPublicadoCommand"/> (convention-based
/// Wolverine): confere que nenhuma versão viva do mesmo código intercepta a janela
/// informada, cria o agregado, persiste e commita.
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
        IPublicacoesUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
            command.Codigo, command.VigenciaInicio, command.VigenciaFim, null, cancellationToken)
            .ConfigureAwait(false);
        if (sobreposta)
        {
            return Result<Guid>.Failure(VigenciaSobrepostaErro(command.Codigo));
        }

        Result<TipoAtoPublicado> tipoResult = TipoAtoPublicado.Criar(
            command.Codigo,
            command.Nome,
            command.CongelaConfiguracao,
            command.UnicoPorObjeto,
            command.EfeitoIrreversivel,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.BaseLegal);

        if (tipoResult.IsFailure)
        {
            return Result<Guid>.Failure(tipoResult.Error!);
        }

        TipoAtoPublicado tipo = tipoResult.Value!;
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExclusionConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && ExclusionConstraintViolation.IsVigenciaConflict(constraint))
        {
            return Result<Guid>.Failure(VigenciaSobrepostaErro(command.Codigo));
        }

        return Result<Guid>.Success(tipo.Id);
    }

    internal static DomainError VigenciaSobrepostaErro(string codigo) =>
        new(TipoAtoPublicadoErrorCodes.VigenciaSobreposta,
            $"Já existe uma versão viva do tipo de ato '{codigo}' vigente em parte do período informado.");
}
