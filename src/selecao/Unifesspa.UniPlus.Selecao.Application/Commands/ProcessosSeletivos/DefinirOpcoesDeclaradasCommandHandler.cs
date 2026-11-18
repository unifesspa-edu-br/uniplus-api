namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeclaradasCommand"/> (issue #1619): confere no catálogo que
/// a fonte dos valores do fato é o processo e entrega as opções ao agregado, que recusa o fato
/// gerido pelo atendimento especializado, a lista vazia, o código repetido e a remoção de opção
/// citada por exigência viva.
/// </summary>
public static class DefinirOpcoesDeclaradasCommandHandler
{
    public static async Task<Result> Handle(
        DefinirOpcoesDeclaradasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
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
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Fora do rascunho a recusa vem antes de consultar cadastros.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
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
            return Result.ValidationFailure(erros);
        }

        FatoCandidatoView? fato = await fatoCandidatoReader
            .ObterPorCodigoAsync(command.FatoCodigo.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (fato is null || !VocabularioDeFatos.OpcoesDoProcesso(fato))
        {
            return Result.Failure(new DomainError(
                OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso,
                "Só se declaram opções para um fato categórico cuja fonte dos valores é o processo."));
        }

        if (VinculoDeFato.Usa(fato.Binding, VinculoDeFato.AgregacaoDeGrupo))
        {
            return Result.Failure(new DomainError(
                OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso,
                $"O fato '{fato.Codigo}' é agregado sobre grupo repetível: as opções dele são as do fato de membro."));
        }

        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        string[] agregadosDoFato = [.. VocabularioDeFatos.MembroPorAgregado(catalogo)
            .Where(a => string.Equals(a.Value, fato.Codigo, StringComparison.Ordinal))
            .Select(static a => a.Key)];

        Result result = processo.DefinirOpcoesDeclaradas(fato.Codigo, opcoes, agregadosDoFato);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
