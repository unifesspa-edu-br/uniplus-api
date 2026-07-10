namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="AtualizarTipoAtoPublicadoCommand"/>. Revalida a
/// sobreposição apenas quando o código ou a janela mudam — alterar o nome ou os
/// atributos de consequência não pode criar conflito.
/// </summary>
public static class AtualizarTipoAtoPublicadoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IPublicacoesUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoAtoPublicado? tipo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(NaoEncontradoErro());
        }

        if (JanelaOuCodigoMudou(tipo, command))
        {
            bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
                command.Codigo, command.VigenciaInicio, command.VigenciaFim, command.Id, cancellationToken)
                .ConfigureAwait(false);
            if (sobreposta)
            {
                return Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro(command.Codigo));
            }
        }

        Result atualizacao = tipo.Atualizar(
            command.Codigo,
            command.Nome,
            command.CongelaConfiguracao,
            command.UnicoPorObjeto,
            command.EfeitoIrreversivel,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.BaseLegal);

        if (atualizacao.IsFailure)
        {
            return atualizacao;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExclusionConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && ExclusionConstraintViolation.IsVigenciaConflict(constraint))
        {
            return Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro(command.Codigo));
        }

        return Result.Success();
    }

    private static bool JanelaOuCodigoMudou(TipoAtoPublicado tipo, AtualizarTipoAtoPublicadoCommand command) =>
        !string.Equals(tipo.Codigo, command.Codigo.Trim(), StringComparison.Ordinal)
        || tipo.VigenciaInicio != command.VigenciaInicio
        || tipo.VigenciaFim != command.VigenciaFim;

    private static DomainError NaoEncontradoErro() =>
        new(TipoAtoPublicadoErrorCodes.NaoEncontrado, "Tipo de ato não encontrado.");
}
