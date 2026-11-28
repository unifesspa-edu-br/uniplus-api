namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeclaradasCommand"/>: confere no catálogo que
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

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(command, obtido.Value!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        DefinirOpcoesDeclaradasCommand command,
        ProcessoSeletivo processo,
        IFatoCandidatoReader fatoCandidatoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<OpcaoDeclaradaFato>> opcoes = ConferirOpcoes(command);
        FatoCandidatoView? fato = await fatoCandidatoReader.ObterPorCodigoAsync(command.FatoCodigo, cancellationToken).ConfigureAwait(false);
        FieldError[] recusasDoFato = [.. RecusaDoFato(fato)?.Errors ?? [], .. RecusaDoFatoGeridoPeloAtendimento(command.FatoCodigo)];

        // As opções e o fato dependem só do pedido: as conferências rodam e as recusas se somam.
        return opcoes.IsFailure || recusasDoFato.Length > 0
            ? Result.ValidationFailure([.. opcoes.Errors, .. recusasDoFato])
            : await GravarAsync(processo, fato!, opcoes.Value!, fatoCandidatoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result<IReadOnlyList<OpcaoDeclaradaFato>> ConferirOpcoes(DefinirOpcoesDeclaradasCommand command) =>
        command.Opcoes is { } opcoes
            ? ConferirCadaOpcao(command.FatoCodigo, opcoes)
            : Result<IReadOnlyList<OpcaoDeclaradaFato>>.ValidationFailure([new FieldError(
                "opcoes", new DomainError(CodigosDeValidacao.Obrigatorio, "Informe as opções do fato."))]);

    private static Result<IReadOnlyList<OpcaoDeclaradaFato>> ConferirCadaOpcao(string fatoCodigo, IReadOnlyList<OpcaoDeclaradaInput> opcoes)
    {
        List<(int Ordem, Result<OpcaoDeclaradaFato> Opcao)> criadas =
            [.. opcoes.Select((input, ordem) => (ordem, OpcaoDeclaradaFato.Criar(fatoCodigo, input.Codigo, input.Rotulo, ordem)))];
        FieldError[] erros = [.. criadas.SelectMany(static c => c.Opcao.Errors.Select(e => e.NoCaminhoDoItem($"opcoes[{c.Ordem}]")))];
        return erros.Length > 0
            ? Result<IReadOnlyList<OpcaoDeclaradaFato>>.ValidationFailure(erros)
            : Result<IReadOnlyList<OpcaoDeclaradaFato>>.Success([.. criadas.Select(static c => c.Opcao.Value!)]);
    }

    private static IEnumerable<FieldError> RecusaDoFatoGeridoPeloAtendimento(string fatoCodigo) =>
        AtendimentoEspecializado.GereOpcoesDoFato(fatoCodigo)
            ? [new FieldError(null, new DomainError(
                OpcaoDeclaradaFatoErrorCodes.GeridasPeloAtendimentoEspecializado,
                "As opções deste fato vêm do atendimento especializado; defina-as nele."))]
            : [];

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
                "O fato é agregado sobre grupo repetível: as opções dele são as do fato de membro."))
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
