namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeclaradasCommand"/> (issue #1619): confere no catálogo que
/// a fonte dos valores do fato é o processo e entrega as opções ao agregado, que recusa o fato
/// gerido pela oferta de atendimento, a lista vazia, o código repetido e a remoção de opção
/// citada por exigência viva.
/// </summary>
public static class DefinirOpcoesDeclaradasCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirOpcoesDeclaradasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição precede as regras de negócio (ADR-0110 D9), como nos demais Definir*.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        List<FieldError> erros = [];
        List<OpcaoDeclaradaFato> opcoes = [];
        for (int ordem = 0; ordem < command.Opcoes.Count; ordem++)
        {
            OpcaoDeclaradaInput input = command.Opcoes[ordem];
            Result<OpcaoDeclaradaFato> opcao = OpcaoDeclaradaFato.Criar(command.FatoCodigo, input.Codigo, input.Rotulo, ordem);
            if (opcao.IsFailure)
            {
                erros.AddRange(opcao.Errors.Select(e => new FieldError($"opcoes[{ordem}].{e.Field}", e.Error)));
                continue;
            }

            opcoes.Add(opcao.Value!);
        }

        if (erros.Count > 0)
        {
            return Result<MutacaoAceita>.ValidationFailure(erros);
        }

        FatoCandidatoView? fato = await fatoCandidatoReader
            .ObterPorCodigoAsync(command.FatoCodigo.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (fato is null || !VocabularioDeFatos.OpcoesDoProcesso(fato))
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso,
                "Só se declaram opções para um fato categórico cuja fonte dos valores é o processo."));
        }

        Result result = processo.DefinirOpcoesDeclaradas(fato.Codigo, opcoes, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
