namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarModalidadeCommand"/>. Pré-checa só os campos
/// independentes de coerência cruzada (parse, tamanhos — 422, sem I/O) ANTES de
/// buscar a modalidade por Id — validação sempre vence 404. As três invariantes
/// cruzadas NÃO entram no pré-check: precisam vir depois da guarda do catálogo
/// legal fixo, que só <see cref="Modalidade.Atualizar"/> pode avaliar, pois
/// depende do <c>Codigo</c> já persistido — reportar uma incoerência antes do
/// fetch preemptiria "esta modalidade não se edita" com "corrija a coerência do
/// payload" quando as duas violações coexistem. Depois do fetch (404),
/// <see cref="Modalidade.Atualizar"/> aplica a guarda e só então as coerências, na
/// ordem certa. Por fim, revalida a integridade referencial dos códigos citados
/// (422, DB) e commita.
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

        Result preCheck = Modalidade.ValidarCamposDoPayload(
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

        if (preCheck.IsFailure)
        {
            return Result.ValidationFailure(preCheck.Errors);
        }

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
            // modalidade.Atualizar já mutou o agregado rastreado — descarta antes de
            // devolver a falha, senão o save automático do Wolverine persistiria a
            // alteração cuja referência acabou de ser recusada.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
                "Um ou mais códigos de modalidade referenciados (origem ou remanejamento) "
                + "não correspondem a modalidades vivas."));
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
