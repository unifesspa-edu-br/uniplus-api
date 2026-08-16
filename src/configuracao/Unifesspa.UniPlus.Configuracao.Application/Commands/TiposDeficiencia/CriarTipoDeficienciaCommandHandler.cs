namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoDeficienciaCommand"/> (convention-based
/// Wolverine): valida o agregado por inteiro primeiro (sem I/O) — nome e
/// descrição acumulam no mesmo lote — só então confere a unicidade do nome
/// entre vivos, com o nome já normalizado. Protege a corrida check-then-act
/// traduzindo a violação do índice único parcial em <c>NomeJaExiste</c>.
/// </summary>
public static class CriarTipoDeficienciaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoDeficiencia> criar = TipoDeficiencia.Criar(command.Nome, command.Descricao, command.Permanente);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        TipoDeficiencia tipo = criar.Value!;

        if (await repository.NomeExisteEntreVivosAsync(tipo.Nome, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(NomeJaExisteErro());
        }

        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(NomeJaExisteErro());
        }

        return Result<Guid>.Success(tipo.Id);
    }

    private static DomainError NomeJaExisteErro() =>
        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            "Já existe um tipo de deficiência vivo com o nome informado.");
}
