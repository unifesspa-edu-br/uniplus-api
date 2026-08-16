namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoBancaCommand"/>. Valida os campos (sem I/O)
/// antes de buscar a entidade: sem o validator removido, um payload mal formado
/// não pode passar a chegar a <c>ObterPorIdAsync</c> primeiro — validação sempre
/// vence sobre "não encontrado", mesma prioridade que o validator garantia. Só um
/// comando já confirmado válido consulta a existência. O <c>Codigo</c> é imutável,
/// então não há checagem de unicidade nem corrida de índice aqui. Sem integridade
/// referencial — o tipo de banca não é referenciado por FK intra-banco.
/// </summary>
public static class AtualizarTipoBancaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoBancaCommand command,
        ITipoBancaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? FaseTipica, string? Descricao)> camposResult =
            TipoBanca.ValidarCamposComuns(command.Nome, command.FaseTipica, command.Descricao);
        if (camposResult.IsFailure)
        {
            return Result.ValidationFailure(camposResult.Errors);
        }

        TipoBanca? banca = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (banca is null)
        {
            return Result.Failure(new DomainError(
                TipoBancaErrorCodes.NaoEncontrado,
                "Tipo de banca não encontrado."));
        }

        // Revalida por dentro (barato, sem I/O) com exatamente os mesmos argumentos
        // já confirmados acima, então sempre terá sucesso aqui; esta chamada só
        // serve para aplicar a mutação.
        banca.Atualizar(command.Nome, command.FaseTipica, command.Descricao);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
