namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDeficienciaCommand"/>. Como o nome é
/// editável, confere a unicidade entre tipos vivos quando ele muda (ignorando o
/// próprio registro) e protege a corrida traduzindo a violação do índice único
/// parcial em <c>NomeJaExiste</c>.
/// </summary>
public static class AtualizarTipoDeficienciaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoDeficiencia? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NaoEncontrado,
                "Tipo de deficiência não encontrado."));
        }

        // Nome é case-sensitive (Ordinal), normalizado por Trim no agregado — só
        // checa colisão quando o nome efetivamente muda.
        if (!string.Equals(command.Nome.Trim(), tipo.Nome, StringComparison.Ordinal)
            && await repository.NomeExisteEntreVivosAsync(command.Nome, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(NomeJaExisteErro(command.Nome));
        }

        Result atualizarResult = tipo.Atualizar(command.Nome, command.Descricao, command.Permanente);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo NomeJaExiste do caminho não-race.
            return Result.Failure(NomeJaExisteErro(command.Nome));
        }

        return Result.Success();
    }

    private static DomainError NomeJaExisteErro(string nome) =>
        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            $"Já existe um tipo de deficiência vivo com o nome '{nome}'.");
}
