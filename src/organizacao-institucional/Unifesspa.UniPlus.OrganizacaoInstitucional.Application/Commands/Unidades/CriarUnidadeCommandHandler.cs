namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="CriarUnidadeCommand"/>. Valida o payload por inteiro antes de
/// qualquer I/O (validação sempre vence I/O), confere a unicidade de Slug, Sigla e Codigo entre
/// unidades vivas e a existência do superior, cria o agregado e persiste; ao final, invalida o
/// cache do reader cross-módulo (ADR-0056).
/// </summary>
public static class CriarUnidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarUnidadeCommand command,
        IUnidadeRepository repository,
        IUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);

        Result validacao = Unidade.ValidarCampos(
            command.Nome, command.Identificadores(), command.Cidade(), command.Tipo, command.Vigencia());
        if (validacao.IsFailure)
        {
            return Result<Guid>.ValidationFailure(validacao.Errors);
        }

        Result conflito = await ValidarConflitosAsync(command, repository, cancellationToken).ConfigureAwait(false);
        if (conflito.IsFailure)
        {
            return Result<Guid>.Failure(conflito.Error!);
        }

        Unidade unidade = CriarUnidade(command);

        await repository.AdicionarAsync(unidade, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(unidade.Id);
    }

    private static Unidade CriarUnidade(CriarUnidadeCommand command) =>
        Unidade.Criar(
            command.Nome,
            command.Identificadores(),
            command.Cidade(),
            command.Vigencia(),
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica).Value!;

    private static async Task<Result> ValidarConflitosAsync(
        CriarUnidadeCommand command,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        Slug slug = Slug.From(command.Slug).Value!;

        if (await repository.SlugExisteEntreLivosAsync(slug, null, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.SlugJaExiste, $"Já existe uma Unidade viva com o slug '{slug}'.");
        }

        if (await repository.SiglaExisteEntreLivosAsync(command.Sigla!, null, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.SiglaJaExiste, $"Já existe uma Unidade viva com a sigla '{command.Sigla}'.");
        }

        if (await repository.CodigoExisteEntreLivosAsync(command.Codigo!, null, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.CodigoJaExiste, $"Já existe uma Unidade viva com o código '{command.Codigo}'.");
        }

        return command.UnidadeSuperiorId is { } superiorId
            && await repository.ObterPorIdAsync(superiorId, cancellationToken).ConfigureAwait(false) is null
            ? Falha(UnidadeErrorCodes.SuperiorNaoEncontrado, "A Unidade superior informada não foi encontrada.")
            : Result.Success();
    }

    private static Result Falha(string codigo, string mensagem) => Result.Failure(new DomainError(codigo, mensagem));
}
