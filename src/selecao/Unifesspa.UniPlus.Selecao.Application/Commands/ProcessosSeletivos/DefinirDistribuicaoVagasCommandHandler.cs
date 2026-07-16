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
    public static async Task<Result<MutacaoAceita>> Handle(
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
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio
        // que este handler avalia (existência de cadastros, coerência de referências): ela
        // as precede na ordem da ADR-0110 D9. Um cliente com If-Match defasado tem de saber
        // disso antes de sair caçando um cadastro que ele não errou.
        //
        // O que ela NÃO precede é a validação de SCHEMA do payload: o FluentValidation roda
        // como middleware do Wolverine, antes deste handler, e um command malformado morre
        // ali com 422 sem que o guard chegue a rodar. É desvio consciente da D9 — corrigi-lo
        // exigiria carregar o agregado no middleware, o que é pior. O custo é uma rodada
        // extra para quem erra as DUAS coisas ao mesmo tempo; nenhum estado é corrompido.
        //
        // O mesmo guard continua dentro do Definir* do domínio: esta antecipação dá a ordem,
        // não a garantia.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
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
                return Result<MutacaoAceita>.Failure(resultado.Error!);
            }

            distribuicoes.Add(resultado.Value!);
        }

        Result result = processo.DefinirDistribuicaoVagas(distribuicoes, command.Precondicao);
        if (result.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(result.Error!);
        }

        // Agregado tracked (ObterParaMutacaoAsync): a nova coleção e suas
        // filhas (Guid v7 já preenchido) são persistidas por change detection.
        // NÃO chamar DbSet.Update — marcaria os filhos novos como Modified,
        // emitindo UPDATE de linhas nunca inseridas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
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

        Result<ReferenciaRegra?> regraAjusteResult = await ResolverRegraAjusteAsync(
            input, regraCatalogoReader, cancellationToken).ConfigureAwait(false);
        if (regraAjusteResult.IsFailure)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(regraAjusteResult.Error!);
        }

        // Achado Codex (revisão do plano, ADR-0115 §2.1): o quadro traz um código que não
        // é modalidade selecionada é reconciliação de IDs crus — Application, não Domain.
        DomainError? erroQuadroOrfao = input.Quadro
            .Where(q => !input.ModalidadeIds.Contains(q.ModalidadeId))
            .Select(q => new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada",
                $"A modalidade {q.ModalidadeId} no quadro não está entre as modalidades selecionadas desta oferta."))
            .FirstOrDefault();
        if (erroQuadroOrfao is not null)
        {
            return Result<ConfiguracaoDistribuicaoVagas>.Failure(erroQuadroOrfao);
        }

        Dictionary<Guid, int> quadroPorModalidade = input.Quadro.ToDictionary(q => q.ModalidadeId, q => q.Quantidade);

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

            int? quantidadeDeclarada = quadroPorModalidade.TryGetValue(modalidadeId, out int quantidade) ? quantidade : null;

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
                view.BaseLegal ?? string.Empty,
                quantidadeDeclarada);

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
            regraAjusteResult.Value,
            demografica,
            modalidades);
    }

    /// <summary>
    /// Resolve a regra de ajuste (<c>TipoRegra.RegraAjusteDistribuicaoVagas</c>)
    /// no catálogo, quando informada. Ausência é válida — o Domain decide se é
    /// obrigatória conforme o ramo (federal exige, institucional não).
    /// </summary>
    private static async Task<Result<ReferenciaRegra?>> ResolverRegraAjusteAsync(
        ConfiguracaoDistribuicaoVagasInput input,
        IRegraCatalogoReader regraCatalogoReader,
        CancellationToken cancellationToken)
    {
        if (input.RegraAjusteCodigo is null)
        {
            return Result<ReferenciaRegra?>.Success(null);
        }

        RegraCatalogo? regraAjuste = await regraCatalogoReader
            .ObterAsync(input.RegraAjusteCodigo, input.RegraAjusteVersao!, cancellationToken)
            .ConfigureAwait(false);
        if (regraAjuste is null)
        {
            return Result<ReferenciaRegra?>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada",
                $"Regra de ajuste {input.RegraAjusteCodigo}/{input.RegraAjusteVersao} não encontrada no rol_de_regras."));
        }

        if (regraAjuste.Tipo != TipoRegra.RegraAjusteDistribuicaoVagas)
        {
            return Result<ReferenciaRegra?>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido",
                $"A regra {input.RegraAjusteCodigo}/{input.RegraAjusteVersao} não é do tipo regra_ajuste_distribuicao_vagas."));
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(regraAjuste.Codigo, regraAjuste.Versao, regraAjuste.Hash);
        return referencia.IsFailure
            ? Result<ReferenciaRegra?>.Failure(referencia.Error!)
            : Result<ReferenciaRegra?>.Success(referencia.Value);
    }
}
