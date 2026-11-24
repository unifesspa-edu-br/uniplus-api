namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
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

        ProcessoSeletivo? processo = await processoSeletivoRepository.ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return RecusaDoProcesso(processo, command.ProcessoSeletivoId)
            ?? await DefinirAsync(command, processo!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    // Fora do rascunho a recusa vem antes de consultar cadastros.
    private static Result? RecusaDoProcesso(ProcessoSeletivo? processo, Guid processoSeletivoId) => processo switch
    {
        null => Result.Failure(new DomainError("ProcessoSeletivo.NaoEncontrado", $"Processo Seletivo {processoSeletivoId} não encontrado.")),
        _ => processo.MutacaoBloqueada() is { } bloqueio ? Result.Failure(bloqueio) : null,
    };

    private static async Task<Result> DefinirAsync(
        DefinirOpcoesDeclaradasCommand command,
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<OpcaoDeclaradaFato>> opcoes = ConferirOpcoes(command);
        if (opcoes.IsFailure)
        {
            return Result.ValidationFailure(opcoes.Errors);
        }

        FatoCandidatoView? fato = await fatoCandidatoReader.ObterPorCodigoAsync(command.FatoCodigo.Trim(), cancellationToken).ConfigureAwait(false);
        return RecusaDoFato(fato) ?? await GravarAsync(processo, fato!, opcoes.Value!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result<IReadOnlyList<OpcaoDeclaradaFato>> ConferirOpcoes(DefinirOpcoesDeclaradasCommand command)
    {
        List<(int Ordem, Result<OpcaoDeclaradaFato> Opcao)> criadas =
            [.. command.Opcoes.Select((input, ordem) => (ordem, OpcaoDeclaradaFato.Criar(command.FatoCodigo, input.Codigo, input.Rotulo, ordem)))];
        FieldError[] erros = [.. criadas.SelectMany(static c => c.Opcao.Errors.Select(e => new FieldError($"opcoes[{c.Ordem}].{e.Field}", e.Error)))];
        return erros.Length > 0
            ? Result<IReadOnlyList<OpcaoDeclaradaFato>>.ValidationFailure(erros)
            : Result<IReadOnlyList<OpcaoDeclaradaFato>>.Success([.. criadas.Select(static c => c.Opcao.Value!)]);
    }

    private static Result? RecusaDoFato(FatoCandidatoView? fato)
    {
        if (fato is null || !VocabularioDeFatos.OpcoesDoProcesso(fato))
        {
            return Result.Failure(new DomainError(
                OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso,
                "Só se declaram opções para um fato categórico cuja fonte dos valores é o processo."));
        }

        return VinculoDeFato.Usa(fato.Binding, MecanismoDoVinculo.ResumoDeGrupo)
            ? Result.Failure(new DomainError(
                OpcaoDeclaradaFatoErrorCodes.FonteNaoEhDoProcesso,
                $"O fato '{fato.Codigo}' é agregado sobre grupo repetível: as opções dele são as do fato de membro."))
            : null;
    }

    private static async Task<Result> GravarAsync(
        ProcessoSeletivo processo,
        FatoCandidatoView fato,
        IReadOnlyList<OpcaoDeclaradaFato> opcoes,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definida = processo.DefinirOpcoesDeclaradas(fato.Codigo, opcoes, await AgregadosDoFatoAsync(fato, fatoCandidatoReader, cancellationToken).ConfigureAwait(false));
        if (definida.IsFailure)
        {
            return Result.Failure(definida.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static async Task<string[]> AgregadosDoFatoAsync(FatoCandidatoView fato, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader.ListarAsync(cancellationToken).ConfigureAwait(false);
        return [.. VocabularioDeFatos.MembroPorAgregado(catalogo)
            .Where(a => string.Equals(a.Value, fato.Codigo, StringComparison.Ordinal))
            .Select(static a => a.Key)];
    }
}
