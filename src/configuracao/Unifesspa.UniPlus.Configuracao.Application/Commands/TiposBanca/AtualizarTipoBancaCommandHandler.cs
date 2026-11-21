namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposBanca;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
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
    private static readonly DomainError NaoEncontrado = new(TipoBancaErrorCodes.NaoEncontrado, "Tipo de banca não encontrado.");

    public static async Task<Result> Handle(
        AtualizarTipoBancaCommand command,
        ITipoBancaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? FaseTipica, string? Descricao, AtuacaoDoTipoDeBanca Atuacao)> camposResult =
            TipoBanca.ValidarCampos(command.Declaracao());

        return camposResult.IsFailure
            ? Result.ValidationFailure(camposResult.Errors)
            : await AtualizarAsync(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarAsync(
        AtualizarTipoBancaCommand command,
        ITipoBancaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        TipoBanca? banca = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (banca is null)
        {
            return Result.Failure(NaoEncontrado);
        }

        // Revalida por dentro (barato, sem I/O) com exatamente os mesmos argumentos
        // já confirmados por quem chama, então sempre terá sucesso aqui; esta chamada só
        // serve para aplicar a mutação.
        banca.Atualizar(command.Declaracao());

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
