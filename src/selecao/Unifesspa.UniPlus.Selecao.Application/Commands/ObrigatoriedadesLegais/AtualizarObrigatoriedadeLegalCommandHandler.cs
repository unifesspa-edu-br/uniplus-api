namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based do <see cref="AtualizarObrigatoriedadeLegalCommand"/>. Semântica full-replace
/// (ADR-0058 Emenda 1): o caller repassa todos os campos. Confere os campos antes de buscar a regra, e só muda
/// a regra rastreada depois de todas as recusas, para que uma recusa não deixe estado alterado no contexto.
/// </summary>
public static class AtualizarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ConferenciaDaObrigatoriedadeLegal conferencia,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(conferencia);
        Result campos = ConferenciaDaObrigatoriedadeLegal.ConferirCampos(command);

        return campos.IsFailure
            ? campos
            : await ConferirEAtualizarAsync(command, repository, conferencia, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result> ConferirEAtualizarAsync(
        AtualizarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ConferenciaDaObrigatoriedadeLegal conferencia,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ObrigatoriedadeLegal? regra = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(RecusasDaObrigatoriedadeLegal.NaoEncontrada(command.Id));
        }

        Result<PredicadoObrigatoriedade> predicado = await conferencia
            .ConferirReferenciasAsync(command, cancellationToken).ConfigureAwait(false);

        return predicado.IsFailure
            ? Result.Failure(predicado.Error!)
            : await AtualizarEGravarAsync(command, regra, predicado.Value!, repository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarEGravarAsync(
        AtualizarObrigatoriedadeLegalCommand command,
        ObrigatoriedadeLegal regra,
        PredicadoObrigatoriedade predicado,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result unica = await GravacaoDaObrigatoriedadeLegal
            .ConferirUnicidadeAsync(repository, command.RegraCodigo, command.Id, cancellationToken).ConfigureAwait(false);
        if (unica.IsFailure)
        {
            return unica;
        }

        Aplicar(command, regra, predicado);
        repository.Atualizar(regra);

        return await GravacaoDaObrigatoriedadeLegal
            .SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Os campos já passaram pela conferência do domínio, então a atualização não falha.
    private static void Aplicar(
        AtualizarObrigatoriedadeLegalCommand command,
        ObrigatoriedadeLegal regra,
        PredicadoObrigatoriedade predicado) =>
        regra.Atualizar(
            command.TipoProcessoCodigo,
            CodigoLidoDoPedido.Ler<CategoriaObrigatoriedade>(command.Categoria),
            command.RegraCodigo,
            predicado,
            command.ComoTextoNormativo(),
            command.VigenciaInicio,
            command.VigenciaFim);
}
