namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based do <see cref="CriarObrigatoriedadeLegalCommand"/>. Confere os campos, depois as
/// referências e a unicidade do código, e só então cria a regra com o predicado já normalizado. A regra é
/// cross-cutting por tipo de processo — sem proprietário nem áreas de interesse.
/// </summary>
public static class CriarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ConferenciaDaObrigatoriedadeLegal conferencia,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(conferencia);
        Result campos = ConferenciaDaObrigatoriedadeLegal.ConferirCampos(command);

        return campos.IsFailure
            ? Result<Guid>.ValidationFailure(campos.Errors)
            : await ConferirEGravarAsync(command, repository, conferencia, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> ConferirEGravarAsync(
        CriarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        ConferenciaDaObrigatoriedadeLegal conferencia,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<PredicadoObrigatoriedade> predicado = await conferencia
            .ConferirReferenciasAsync(command, cancellationToken).ConfigureAwait(false);

        return predicado.IsFailure
            ? Result<Guid>.Failure(predicado.Error!)
            : await CriarEGravarAsync(command, predicado.Value!, repository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> CriarEGravarAsync(
        CriarObrigatoriedadeLegalCommand command,
        PredicadoObrigatoriedade predicado,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result unica = await GravacaoDaObrigatoriedadeLegal
            .ConferirUnicidadeAsync(repository, command.RegraCodigo, excluirId: null, cancellationToken).ConfigureAwait(false);
        if (unica.IsFailure)
        {
            return Result<Guid>.Failure(unica.Error!);
        }

        ObrigatoriedadeLegal regra = NovaRegra(command, predicado);
        await repository.AdicionarAsync(regra, cancellationToken).ConfigureAwait(false);
        Result gravado = await GravacaoDaObrigatoriedadeLegal
            .SalvarAsync(unitOfWork, command.RegraCodigo, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure ? Result<Guid>.Failure(gravado.Error!) : Result<Guid>.Success(regra.Id);
    }

    // Os campos já passaram pela conferência do domínio, então a criação não falha.
    private static ObrigatoriedadeLegal NovaRegra(CriarObrigatoriedadeLegalCommand command, PredicadoObrigatoriedade predicado) =>
        ObrigatoriedadeLegal.Criar(
            command.TipoProcessoCodigo,
            command.Categoria,
            command.RegraCodigo,
            predicado,
            command.ComoTextoNormativo(),
            command.VigenciaInicio,
            command.VigenciaFim).Value!;
}
