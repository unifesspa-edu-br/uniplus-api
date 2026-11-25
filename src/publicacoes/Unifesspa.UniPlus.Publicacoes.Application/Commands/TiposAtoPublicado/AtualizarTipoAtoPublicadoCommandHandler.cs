namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="AtualizarTipoAtoPublicadoCommand"/>. Valida os campos
/// independentes do payload ANTES de qualquer I/O — validação sempre vence 404 —
/// e só então busca o tipo por Id (a checagem de imutabilidade do código depende
/// do valor persistido, então roda depois do fetch). Revalida a sobreposição
/// apenas quando o código ou a janela mudam — alterar o nome ou os atributos de
/// consequência não pode criar conflito.
/// </summary>
public static class AtualizarTipoAtoPublicadoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        (TipoAtoPublicado? tipo, Result verificacao) = await PrepararAtualizacao(
            command, repository, cancellationToken).ConfigureAwait(false);
        if (verificacao.IsFailure)
        {
            return verificacao;
        }

        return await Atualizar(command, tipo!, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> Atualizar(
        AtualizarTipoAtoPublicadoCommand command, TipoAtoPublicado tipo,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result atualizacao = tipo.Atualizar(command.Declaracao());
        if (atualizacao.IsFailure)
        {
            return atualizacao;
        }

        return await Salvar(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(TipoAtoPublicado? Tipo, Result Verificacao)> PrepararAtualizacao(
        AtualizarTipoAtoPublicadoCommand command, ITipoAtoPublicadoRepository repository,
        CancellationToken cancellationToken)
    {
        Result<CamposDoTipoAtoPublicado> validacaoPreCheck = TipoAtoPublicado.ValidarDeclaracao(command.Declaracao());
        if (validacaoPreCheck.IsFailure)
        {
            return (null, Result.ValidationFailure(validacaoPreCheck.Errors));
        }

        TipoAtoPublicado? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return (null, Result.Failure(NaoEncontradoErro()));
        }

        Result verificacao = await VerificarAtualizacao(command, tipo, repository, cancellationToken).ConfigureAwait(false);
        return verificacao.IsFailure ? (null, verificacao) : (tipo, Result.Success());
    }

    private static async Task<Result> VerificarAtualizacao(
        AtualizarTipoAtoPublicadoCommand command, TipoAtoPublicado tipo,
        ITipoAtoPublicadoRepository repository, CancellationToken cancellationToken)
    {
        Result codigo = tipo.ConferirCodigoInalterado(command.Codigo);
        if (codigo.IsFailure)
        {
            return codigo;
        }

        return await VerificarSobreposicao(command, tipo, repository, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> VerificarSobreposicao(
        AtualizarTipoAtoPublicadoCommand command, TipoAtoPublicado tipo,
        ITipoAtoPublicadoRepository repository, CancellationToken cancellationToken)
    {
        if (!JanelaMudou(tipo, command))
        {
            return Result.Success();
        }

        bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
            tipo.Codigo, command.VigenciaInicio, command.VigenciaFim, command.Id, cancellationToken)
            .ConfigureAwait(false);
        return sobreposta
            ? Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro())
            : Result.Success();
    }

    private static async Task<Result> Salvar(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
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

        return Result.Success();
    }

    // O outbox salva novamente após o retorno; descartar evita repetir o conflito fora do catch.
    private static Result DescartarAlteracoesComConflito(IUnitOfWork unitOfWork)
    {
        unitOfWork.DescartarAlteracoesNaoSalvas();
        return Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro());
    }

    private static bool JanelaMudou(TipoAtoPublicado tipo, AtualizarTipoAtoPublicadoCommand command) =>
        tipo.VigenciaInicio != command.VigenciaInicio || tipo.VigenciaFim != command.VigenciaFim;

    private static DomainError NaoEncontradoErro() =>
        new(TipoAtoPublicadoErrorCodes.NaoEncontrado, "Tipo de ato não encontrado.");
}
