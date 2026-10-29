namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

using Wolverine.Attributes;

/// <summary>
/// Handler do <see cref="AtualizarFaseCanonicaCommand"/>. Valida antes de I/O só o
/// que é determinável sem o código persistido (os quatro campos comuns) — as
/// coerências que dependem do código (<c>AgrupaEtapas</c>,
/// <c>PermiteComplementacao</c>) só podem ser avaliadas depois do fetch, porque o
/// comando não carrega o código (imutável). Carrega a fase (404), então chama
/// <c>Atualizar</c>, que revalida o lote inteiro — inclusive as duas coerências
/// dependentes — contra o código congelado. Sem integridade referencial — a fase
/// não é referenciada por FK intra-banco.
/// </summary>
public static class AtualizarFaseCanonicaCommandHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c> porque o handler injeta o reader de Organização e o unit of work de
    /// Configuração, e o detector de transação do Wolverine falha por ambiguidade de <c>DbContext</c>.
    /// </summary>
    [NonTransactional]
    public static async Task<Result> Handle(
        AtualizarFaseCanonicaCommand command,
        IFaseCanonicaRepository repository,
        IUnidadeReader unidadeReader,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unidadeReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? Descricao, string? BaseLegal, OrigemDataFase OrigemData)> comuns =
            FaseCanonica.ValidarCamposComuns(
                command.Nome, command.Descricao, command.BaseLegal, command.OrigemData);
        if (comuns.IsFailure)
        {
            return Result.ValidationFailure(comuns.Errors);
        }

        FaseCanonica? fase = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (fase is null)
        {
            return Result.Failure(new DomainError(
                FaseCanonicaErrorCodes.NaoEncontrada,
                "Fase canônica não encontrada."));
        }

        Result atualizarResult = fase.Atualizar(
            command.Nome,
            command.Descricao,
            command.AgrupaEtapas,
            command.PermiteComplementacao,
            command.BaseLegal,
            command.ColetaInscricao,
            command.ColetaSolicitacaoIsencao,
            command.OrigemData);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        // Reenviar a unidade já gravada preserva a cópia: a unidade pode ter sido desativada
        // depois, e isso não deve impedir a edição dos outros campos. Só a troca é conferida.
        if (command.UnidadeSugeridaId != fase.UnidadeSugeridaOrigemId)
        {
            Result<UnidadeSugerida?> unidade = await UnidadeSugeridaResolver
                .ResolverAsync(command.UnidadeSugeridaId, unidadeReader, cancellationToken)
                .ConfigureAwait(false);
            if (unidade.IsFailure)
            {
                return Result.ValidationFailure(unidade.Errors);
            }

            fase.DefinirUnidadeSugerida(unidade.Value);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
