namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarModalidadeCommand"/>. Carrega a modalidade (404
/// se inexistente), aplica os campos editáveis (o <c>Codigo</c> é imutável, então
/// não há checagem de unicidade nem corrida de índice), revalida as invariantes de
/// coerência (422) e a integridade referencial dos códigos citados (422), e commita.
/// </summary>
public static class AtualizarModalidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarModalidadeCommand command,
        IModalidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Modalidade? modalidade = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (modalidade is null)
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.NaoEncontrada,
                "Modalidade de concorrência não encontrada."));
        }

        Result atualizarResult = modalidade.Atualizar(
            command.Descricao,
            command.NaturezaLegal,
            command.ComposicaoVagas,
            command.ComposicaoOrigem,
            command.RegraRemanejamento,
            command.RemanejamentoDestino,
            command.RemanejamentoPar,
            command.RemanejamentoFallback,
            command.CriteriosCumulativos,
            command.AcaoQuandoIndeferido,
            command.BaseLegal);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        // Integridade referencial (invariante 7): todos os códigos citados após a
        // atualização devem existir como modalidade viva.
        IReadOnlyCollection<string> referencias = ReferenciasDeModalidade.Coletar(modalidade);
        if (referencias.Count > 0
            && !await repository.CodigosVivosExistemAsync(referencias, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
                "Um ou mais códigos de modalidade referenciados (origem ou remanejamento) "
                + "não correspondem a modalidades vivas."));
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
