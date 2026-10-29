namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da criação de perfil: valida o payload por inteiro no agregado antes de qualquer
/// I/O e só então confere a unicidade do nome já normalizado.
/// </summary>
public static class CriarPerfilCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarPerfilCommand command,
        IPerfilRepository repository,
        ICatalogoDePermissoesConsultavel catalogo,
        IAutorizacaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<Perfil> criar = Perfil.Criar(command.Nome, command.Descricao, command.Permissoes, catalogo);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        Perfil perfil = criar.Value!;

        if (await repository.NomeExisteAsync(perfil.Nome, exceto: null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(PerfilHandlerErros.NomeDuplicado());
        }

        await repository.AdicionarAsync(perfil, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeNome(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(PerfilHandlerErros.NomeDuplicado());
        }

        return Result<Guid>.Success(perfil.Id);
    }
}
