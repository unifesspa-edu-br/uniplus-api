namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class AtualizarInstituicaoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarInstituicaoCommand command,
        IInstituicaoRepository repository,
        IUnidadeRepository unidadeRepository,
        IUnitOfWork unitOfWork,
        IInstituicaoCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Instituicao? instituicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (instituicao is null)
        {
            return Result.Failure(new DomainError(
                InstituicaoErrorCodes.NaoEncontrada,
                "Instituição não encontrada."));
        }

        DomainError? vinculoInvalido = await InstituicaoUnidadeRaizGuard
            .ValidarAsync(command.UnidadeRaizId, unidadeRepository, cancellationToken)
            .ConfigureAwait(false);
        if (vinculoInvalido is not null)
        {
            return Result.Failure(vinculoInvalido);
        }

        Result atualizarResult = instituicao.Atualizar(
            command.CodigoEmec,
            command.Nome,
            command.Sigla,
            command.OrganizacaoAcademica,
            command.CategoriaAdministrativa,
            command.Cnpj,
            command.Mantenedora,
            command.CodigoMantenedoraEmec,
            command.Situacao,
            command.AtoCredenciamento,
            command.AtoRecredenciamento,
            command.ConceitoInstitucional,
            command.Igc,
            command.Website,
            command.EnderecoSede,
            command.MunicipioSede,
            command.UnidadeRaizId);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
