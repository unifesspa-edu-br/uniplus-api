namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve um <see cref="ConfiguracaoDistribuicaoVagasInput"/> cru (Ids e
/// códigos do payload do cliente) contra os cadastros vivos do módulo
/// Configuração e o catálogo <c>rol_de_regras</c>, montando uma
/// <see cref="ConfiguracaoDistribuicaoVagas"/> congelada por valor
/// (snapshot-copy, ADR-0061).
/// </summary>
/// <remarks>
/// Extraído de <see cref="DefinirDistribuicaoVagasCommandHandler"/> (issue
/// #1282/#1283) para ser reaproveitado, sem duplicação, pelo comando que
/// persiste a distribuição e pela query que só simula o cálculo — as duas
/// superfícies precisam concordar byte a byte sobre o que é uma
/// configuração válida e sobre o quadro que ela produz.
/// </remarks>
internal static class ConfiguracaoDistribuicaoVagasResolver
{
    /// <summary>
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/> reporta violações do conjunto de
    /// modalidades sob o field <c>modalidades</c> — o nome da propriedade do agregado
    /// (<see cref="ConfiguracaoDistribuicaoVagas.Modalidades"/>), não do payload do cliente,
    /// que expõe <see cref="ConfiguracaoDistribuicaoVagasInput.ModalidadeIds"/>. Sem esta
    /// tradução, a resposta de validação apontaria o cliente para uma propriedade
    /// inexistente no request.
    /// </summary>
    internal static string TraduzirFieldDoDominio(string fieldDominio) =>
        fieldDominio switch
        {
            "modalidades" => "modalidadeIds",
            _ => fieldDominio,
        };

    internal static async Task<Result<ConfiguracaoDistribuicaoVagas>> ResolverDistribuicaoAsync(
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

        // ADR-0115 §2.1: o quadro traz um código que não
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
            modalidades,
            oferta.VagasAnuaisAutorizadas,
            ModalidadesAdmitidasDoEsquemaArgs.Extrair(regra),
            input.ArgsAjuste);
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
