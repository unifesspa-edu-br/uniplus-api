namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoDocumentoCommand"/> (convention-based
/// Wolverine): valida o agregado por inteiro primeiro (sem I/O), só então confere
/// a unicidade do código entre tipos vivos, com o código já normalizado, cria,
/// persiste e commita. Protege a corrida check-then-act traduzindo a violação do
/// índice único parcial em <c>CodigoJaExiste</c> (CA-02).
/// </summary>
public static class CriarTipoDocumentoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoDocumentoCommand command,
        ITipoDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoDocumento> tipoResult = TipoDocumento.Criar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.Categoria,
            command.FormatosAceitos,
            command.TamanhoMaximoMb,
            command.TipoEquivalente);

        if (tipoResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(tipoResult.Errors);
        }

        TipoDocumento tipo = tipoResult.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(tipo.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(tipo.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(TipoDocumentoErrorCodes.CodigoJaExiste,
            "Já existe um tipo de documento vivo com o código informado.");
}
