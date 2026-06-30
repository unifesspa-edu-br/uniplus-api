namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDocumentoCommand"/>. Como o código é
/// editável, confere a unicidade entre tipos vivos quando ele muda (ignorando o
/// próprio registro) e protege a corrida traduzindo a violação do índice único
/// parcial em <c>CodigoJaExiste</c> (CA-02).
/// </summary>
public static class AtualizarTipoDocumentoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoDocumentoCommand command,
        ITipoDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoDocumento? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDocumentoErrorCodes.NaoEncontrado,
                "Tipo de documento não encontrado."));
        }

        // Código é case-sensitive (Ordinal), normalizado por Trim no agregado — só
        // checa colisão quando o código efetivamente muda.
        if (!string.Equals(command.Codigo.Trim(), tipo.Codigo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result atualizarResult = tipo.Atualizar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.Categoria,
            command.FormatosAceitos,
            command.TamanhoMaximoMb,
            command.TipoEquivalente);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo CodigoJaExiste do caminho não-race.
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(TipoDocumentoErrorCodes.CodigoJaExiste,
            $"Já existe um tipo de documento vivo com o código '{codigo}'.");
}
