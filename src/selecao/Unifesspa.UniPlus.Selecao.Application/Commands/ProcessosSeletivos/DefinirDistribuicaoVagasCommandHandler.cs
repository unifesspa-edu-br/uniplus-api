namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Handler do <see cref="DefinirDistribuicaoVagasCommand"/> (Story #773):
/// resolve cada oferta e regra de distribuição nos cadastros vivos do módulo
/// Configuração (<see cref="IOfertaCursoReader"/>/<see cref="IModalidadeReader"/>/
/// <see cref="IReferenciaReservaDemograficaReader"/>, ADR-0056) e no catálogo
/// <c>rol_de_regras</c> (<see cref="IRegraCatalogoReader"/>, Story #772),
/// congela cada peça por valor (snapshot-copy, ADR-0061) e monta a
/// distribuição — as invariantes (PR, referência demográfica, modalidades
/// federais, coerência de cada modalidade) são garantidas pelas factories do
/// domínio.
/// </summary>
public static class DefinirDistribuicaoVagasCommandHandler
{
    public static async Task<Result> Handle(
        DefinirDistribuicaoVagasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader,
        IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(ofertaCursoReader);
        ArgumentNullException.ThrowIfNull(modalidadeReader);
        ArgumentNullException.ThrowIfNull(referenciaReservaDemograficaReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        List<ConfiguracaoDistribuicaoVagas> distribuicoes = [];
        foreach (ConfiguracaoDistribuicaoVagasInput input in command.DistribuicaoVagas)
        {
            Result<ConfiguracaoDistribuicaoVagas> resultado = await ResolverDistribuicaoAsync(
                input,
                regraCatalogoReader,
                ofertaCursoReader,
                modalidadeReader,
                referenciaReservaDemograficaReader,
                cancellationToken).ConfigureAwait(false);

            if (resultado.IsFailure)
            {
                return Result.Failure(resultado.Error!);
            }

            distribuicoes.Add(resultado.Value!);
        }

        Result result = processo.DefinirDistribuicaoVagas(distribuicoes);
        if (result.IsFailure)
        {
            return result;
        }

        // Agregado tracked (ObterComConfiguracaoAsync): a nova coleção e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static async Task<Result<ConfiguracaoDistribuicaoVagas>> ResolverDistribuicaoAsync(
        ConfiguracaoDistribuicaoVagasInput input,
        IRegraCatalogoReader regraCatalogoReader,
        IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader,
        IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader,
        CancellationToken cancellationToken)
    {
        OfertaCursoView? oferta = await ofertaCursoReader
            .ObterPorIdAsync(input.OfertaCursoId, cancellationToken)
            .ConfigureAwait(false);
        if (oferta is null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada",
                $"Oferta de curso {input.OfertaCursoId} não encontrada ou não está mais viva."));
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(input.RegraDistribuicaoCodigo, input.RegraDistribuicaoVersao, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada",
                $"Regra de distribuição {input.RegraDistribuicaoCodigo}/{input.RegraDistribuicaoVersao} não encontrada no rol_de_regras."));
        }

        if (regra.Tipo != TipoRegra.RegraDistribuicaoVagas)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido",
                $"A regra {input.RegraDistribuicaoCodigo}/{input.RegraDistribuicaoVersao} não é do tipo regra_distribuicao_vagas."));
        }

        Result<ReferenciaRegra> referenciaRegraResult = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        if (referenciaRegraResult.IsFailure)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(referenciaRegraResult.Error!);
        }

        ReferenciaReservaDemograficaSnapshot? demografica = null;
        if (input.ReferenciaReservaDemograficaId is { } referenciaId)
        {
            ReferenciaReservaDemograficaView? view = await referenciaReservaDemograficaReader
                .ObterPorIdAsync(referenciaId, cancellationToken)
                .ConfigureAwait(false);
            if (view is null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada",
                    $"Referência de reserva demográfica {referenciaId} não encontrada ou não está mais viva."));
            }

            Result<ReferenciaReservaDemograficaSnapshot> snapshotResult = ReferenciaReservaDemograficaSnapshot.Criar(
                view.Id, view.CensoReferencia, view.PpiPercentual, view.QuilombolaPercentual, view.PcdPercentual, view.BaseLegal);
            if (snapshotResult.IsFailure)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(snapshotResult.Error!);
            }

            demografica = snapshotResult.Value;
        }

        List<ModalidadeSelecionada> modalidades = [];
        foreach (Guid modalidadeId in input.ModalidadeIds)
        {
            ModalidadeView? view = await modalidadeReader
                .ObterPorIdAsync(modalidadeId, cancellationToken)
                .ConfigureAwait(false);
            if (view is null)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(new DomainError(
                    "ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada",
                    $"Modalidade {modalidadeId} não encontrada ou não está mais viva."));
            }

            Result<ModalidadeSelecionada> modalidadeResult = ModalidadeSelecionada.Criar(
                view.Id,
                view.Codigo,
                view.Descricao,
                NaturezaLegalModalidadeCodigo.FromCodigo(view.NaturezaLegal),
                ComposicaoVagasModalidadeCodigo.FromCodigo(view.ComposicaoVagas),
                view.ComposicaoOrigem,
                RegraRemanejamentoModalidadeCodigo.FromCodigo(view.RegraRemanejamento),
                view.RemanejamentoDestino,
                view.RemanejamentoPar,
                view.RemanejamentoFallback,
                view.CriteriosCumulativos,
                view.AcaoQuandoIndeferido,
                view.BaseLegal ?? string.Empty);

            if (modalidadeResult.IsFailure)
            {
                return Result<ConfiguracaoDistribuicaoVagas>.Failure(modalidadeResult.Error!);
            }

            modalidades.Add(modalidadeResult.Value!);
        }

        return ConfiguracaoDistribuicaoVagas.Criar(
            input.OfertaCursoId,
            input.VoBase,
            input.Pr,
            referenciaRegraResult.Value!,
            demografica,
            modalidades);
    }
}
