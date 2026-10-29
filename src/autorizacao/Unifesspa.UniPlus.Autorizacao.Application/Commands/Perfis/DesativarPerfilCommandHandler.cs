namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da desativação de perfil. Concessões existentes seguem como estão.</summary>
public static class DesativarPerfilCommandHandler
{
    public static async Task<Result> Handle(
        DesativarPerfilCommand command,
        IPerfilRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Perfil? perfil = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result.Failure(PerfilHandlerErros.NaoEncontrado(command.Id));
        }

        Result resultado = perfil.Desativar();
        if (resultado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return resultado;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeConcorrencia(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }
}
