namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida o agregado por inteiro primeiro — código, nome, descrição e conceitos admitidos acumulam no mesmo
/// lote — e só então confere a unicidade do código entre vivos, com o código já normalizado.
/// </summary>
public static class CriarTipoEtapaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoEtapaCommand command,
        ITipoEtapaRepository repository,
        ITipoConceitoRepository conceitos,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(conceitos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoEtapa> montado = await Montar(command, conceitos, cancellationToken).ConfigureAwait(false);
        return montado.IsFailure
            ? Result<Guid>.ValidationFailure(montado.Errors)
            : await RegistrarSeInedito(montado.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<TipoEtapa>> Montar(
        CriarTipoEtapaCommand command, ITipoConceitoRepository conceitos, CancellationToken cancellationToken)
    {
        Result<TipoEtapa> criar = TipoEtapa.Criar(
            command.Codigo, command.Nome, command.Descricao, command.AdmitePontuacao, command.AdmiteEliminacao);
        Result<IReadOnlyList<TipoConceito>> admitidos = await ResolucaoDosConceitosAdmitidos
            .Resolver(command.ConceitosAdmitidos, conceitos, cancellationToken).ConfigureAwait(false);
        FieldError[] erros = [.. criar.Errors, .. admitidos.Errors];
        if (erros.Length > 0)
        {
            return Result<TipoEtapa>.ValidationFailure(erros);
        }

        criar.Value!.SubstituirConceitosAdmitidos(admitidos.Value!);
        return criar;
    }

    private static async Task<Result<Guid>> RegistrarSeInedito(
        TipoEtapa tipo, ITipoEtapaRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        await repository.CodigoExisteAsync(tipo.Codigo, cancellationToken).ConfigureAwait(false)
            ? Result<Guid>.Failure(CodigoJaExiste())
            : await Persistir(tipo, repository, unitOfWork, cancellationToken).ConfigureAwait(false);

    private static async Task<Result<Guid>> Persistir(
        TipoEtapa tipo, ITipoEtapaRepository repository, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (UniqueConstraintViolation.EhConflitoDeCodigo(exception))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync automático do
            // Wolverine repete a inserção fora deste catch: o 409 pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste());
        }
        return Result<Guid>.Success(tipo.Id);
    }

    private static DomainError CodigoJaExiste() => new(
        TipoEtapaErrorCodes.CodigoJaExiste,
        "Já existe um tipo de etapa com o código informado.");
}
