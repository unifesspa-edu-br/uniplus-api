namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="AtualizarUnidadeCommand"/>. Valida o payload por inteiro
/// antes de qualquer I/O (validação sempre vence 404) e só então busca a Unidade,
/// confere unicidade e hierarquia.
/// </summary>
public static class AtualizarUnidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarUnidadeCommand command,
        IUnidadeRepository repository,
        IUnitOfWork unitOfWork,
        IUnidadeCacheInvalidator cacheInvalidator,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(cacheInvalidator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result payload = ValidarPayload(command);
        if (payload.IsFailure)
        {
            return payload;
        }

        Unidade? unidade = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (unidade is null)
        {
            return Result.Failure(new DomainError(UnidadeErrorCodes.NaoEncontrada, "Unidade não encontrada."));
        }

        Result conflito = await ValidarConflitosAsync(command, unidade, repository, cancellationToken).ConfigureAwait(false);
        Result atualizacao = conflito.IsFailure ? conflito : AtualizarUnidade(unidade, command, timeProvider);
        if (atualizacao.IsFailure)
        {
            return atualizacao;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        await cacheInvalidator.InvalidarAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static async Task<Result> ValidarConflitosAsync(
        AtualizarUnidadeCommand command,
        Unidade unidade,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        Result unicidade = await ValidarUnicidadeAsync(command, unidade, repository, cancellationToken).ConfigureAwait(false);

        return unicidade.IsFailure
            ? unicidade
            : await ValidarSuperiorAsync(command, repository, cancellationToken).ConfigureAwait(false);
    }

    private static Result AtualizarUnidade(Unidade unidade, AtualizarUnidadeCommand command, TimeProvider timeProvider) =>
        unidade.Atualizar(
            command.Nome,
            new MudancaDeIdentificadores(
                command.Identificadores(),
                DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
                command.MotivoMudancaIdentificador),
            command.Cidade(),
            command.VigenciaFim,
            command.UnidadeSuperiorId,
            command.Tipo,
            command.UnidadeAcademica);

    // A coerência entre VigenciaFim e VigenciaInicio exige o valor persistido, então
    // roda de novo dentro de Unidade.Atualizar, depois de buscar a Unidade.
    private static Result ValidarPayload(AtualizarUnidadeCommand command)
    {
        Result campos = Unidade.ValidarCampos(
            command.Nome,
            command.Identificadores(),
            command.Cidade(),
            command.Tipo,
            new VigenciaDaUnidade(DateOnly.MinValue, null));
        Result motivo = UnidadeIdentificadorHistorico.ValidarMotivoMudanca(command.MotivoMudancaIdentificador);

        return campos.IsFailure || motivo.IsFailure
            ? Result.ValidationFailure([.. campos.Errors, .. motivo.Errors])
            : Result.Success();
    }

    private static async Task<Result> ValidarUnicidadeAsync(
        AtualizarUnidadeCommand command,
        Unidade unidade,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        Slug slug = Slug.From(command.Slug).Value!;

        if (!string.Equals(slug.Valor, unidade.Slug.Valor, StringComparison.OrdinalIgnoreCase)
            && await repository.SlugExisteEntreLivosAsync(slug, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.SlugJaExiste, $"Já existe uma Unidade viva com o slug '{slug}'.");
        }

        if (!string.Equals(command.Sigla, unidade.Sigla, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(command.Sigla!, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.SiglaJaExiste, $"Já existe uma Unidade viva com a sigla '{command.Sigla}'.");
        }

        // Codigo é case-sensitive (o agregado preserva a caixa e o índice único também):
        // Ordinal faz ABC→abc disparar a checagem em vez de estourar no índice.
        if (!string.Equals(command.Codigo!.Trim(), unidade.Codigo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreLivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Falha(UnidadeErrorCodes.CodigoJaExiste, $"Já existe uma Unidade viva com o código '{command.Codigo}'.");
        }

        return Result.Success();
    }

    private static async Task<Result> ValidarSuperiorAsync(
        AtualizarUnidadeCommand command,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        if (command.UnidadeSuperiorId is not { } superiorId)
        {
            return Result.Success();
        }

        if (superiorId == command.Id)
        {
            return Falha(UnidadeErrorCodes.SuperiorFormaCiclo, "Uma Unidade não pode ser superior de si mesma.");
        }

        if (await repository.ObterPorIdAsync(superiorId, cancellationToken).ConfigureAwait(false) is null)
        {
            return Falha(UnidadeErrorCodes.SuperiorNaoEncontrado, "A Unidade superior informada não foi encontrada.");
        }

        return await repository.EhDescendenteAsync(superiorId, command.Id, cancellationToken).ConfigureAwait(false)
            ? Falha(
                UnidadeErrorCodes.SuperiorFormaCiclo,
                "A Unidade superior informada é descendente da Unidade sendo editada — formaria ciclo na hierarquia.")
            : Result.Success();
    }

    private static Result Falha(string codigo, string mensagem) => Result.Failure(new DomainError(codigo, mensagem));
}
