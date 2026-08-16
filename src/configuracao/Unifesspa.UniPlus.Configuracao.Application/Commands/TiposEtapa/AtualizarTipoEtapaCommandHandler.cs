namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida nome e descrição (sem I/O) antes de buscar a entidade: sem o validator
/// removido, um payload mal formado não pode chegar a <c>ObterPorIdAsync</c>
/// primeiro — validação sempre vence sobre "não encontrado".
/// </summary>
public static class AtualizarTipoEtapaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoEtapaCommand command,
        ITipoEtapaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? Descricao)> validacao = TipoEtapa.ValidarCamposEditaveis(command.Nome, command.Descricao);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        TipoEtapa? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoEtapaErrorCodes.NaoEncontrado, "Tipo de etapa não encontrado."));
        }

        // Revalida por dentro (barato, sem I/O) com exatamente os mesmos argumentos
        // já confirmados acima, então sempre terá sucesso aqui; esta chamada só
        // serve para aplicar a mutação.
        tipo.Atualizar(command.Nome, command.Descricao);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
