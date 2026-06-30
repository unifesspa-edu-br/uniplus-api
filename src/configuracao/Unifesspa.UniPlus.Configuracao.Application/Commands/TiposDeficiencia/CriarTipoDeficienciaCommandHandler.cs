namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoDeficienciaCommand"/> (convention-based
/// Wolverine): confere a unicidade do nome entre tipos vivos, cria o agregado,
/// persiste e commita. Protege a corrida check-then-act traduzindo a violação do
/// índice único parcial em <c>NomeJaExiste</c>.
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

        if (await repository.NomeExisteEntreVivosAsync(command.Nome, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(NomeJaExisteErro(command.Nome));
        }

        Result<TipoDeficiencia> tipoResult = TipoDeficiencia.Criar(command.Nome, command.Descricao);

        if (tipoResult.IsFailure)
        {
            return Result<Guid>.Failure(tipoResult.Error!);
        }

        TipoDeficiencia tipo = tipoResult.Value!;
        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Corrida entre NomeExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo NomeJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(NomeJaExisteErro(command.Nome));
        }

        return Result<Guid>.Success(tipo.Id);
    }

    private static DomainError NomeJaExisteErro(string nome) =>
        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            $"Já existe um tipo de deficiência vivo com o nome '{nome}'.");
}
