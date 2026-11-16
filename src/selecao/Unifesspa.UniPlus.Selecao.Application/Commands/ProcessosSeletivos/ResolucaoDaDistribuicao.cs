namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Vagas;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// A resolução de uma distribuição de vagas pedida (ids e códigos crus do payload) contra os cadastros vivos
/// e o catálogo de regras, na ordem em que as recusas se anunciam: oferta, regra de distribuição, regra de
/// ajuste, quadro fora das modalidades, referência demográfica e modalidades. Cada etapa devolve a recusa que
/// encontrou, ou nada; o que resolveu fica guardado para a criação da configuração, que congela cada peça por
/// valor (ADR-0061).
/// </summary>
internal sealed class ResolucaoDaDistribuicao(
    ConfiguracaoDistribuicaoVagasInput pedido,
    IRegraCatalogoReader regraCatalogoReader,
    IOfertaCursoReader ofertaCursoReader,
    IModalidadeReader modalidadeReader,
    IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader)
{
    private OfertaCursoView _oferta = null!;
    private OfertaCursoSnapshot _nomeDaOferta = null!;
    private RegraCatalogo _regra = null!;
    private ReferenciaRegra? _regraDeAjuste;
    private ReferenciaReservaDemograficaSnapshot? _demografica;
    private readonly List<ModalidadeSelecionada> _modalidades = [];

    internal async Task<Result<ConfiguracaoDistribuicaoVagas>> ResolverAsync(CancellationToken cancellationToken)
    {
        DomainError? recusa = await ResolverOfertaAsync(cancellationToken).ConfigureAwait(false)
            ?? await ResolverRegraDeDistribuicaoAsync(cancellationToken).ConfigureAwait(false)
            ?? await ResolverRegraDeAjusteAsync(cancellationToken).ConfigureAwait(false)
            ?? ConferirQuadroDasModalidadesSelecionadas()
            ?? await ResolverReferenciaDemograficaAsync(cancellationToken).ConfigureAwait(false)
            ?? await ResolverModalidadesAsync(cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? ConfiguracaoDistribuicaoVagas.Criar(pedido.OfertaCursoId, Declarada(), FatosDoCadastro())
            : Result<ConfiguracaoDistribuicaoVagas>.Failure(recusa);
    }

    private async Task<DomainError?> ResolverOfertaAsync(CancellationToken cancellationToken)
    {
        OfertaCursoView? oferta = await ofertaCursoReader
            .ObterPorIdAsync(pedido.OfertaCursoId, cancellationToken).ConfigureAwait(false);
        if (oferta is null)
        {
            return RecusasDaResolucaoDeVagas.OfertaNaoEncontrada(pedido.OfertaCursoId);
        }

        Result<OfertaCursoSnapshot> nome = OfertaCursoSnapshot.Criar(
            oferta.CursoNome, oferta.CursoGrau, oferta.UnidadeOfertanteNome, oferta.Turnos);
        _oferta = oferta;
        _nomeDaOferta = nome.Value!;
        return nome.IsFailure ? nome.Error : null;
    }

    private async Task<DomainError?> ResolverRegraDeDistribuicaoAsync(CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao, cancellationToken).ConfigureAwait(false);
        if (regra is null)
        {
            return RecusasDaResolucaoDeVagas.RegraDeDistribuicaoNaoEncontrada(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao);
        }

        _regra = regra;
        return regra.Tipo == TipoRegra.RegraDistribuicaoVagas
            ? null
            : RecusasDaResolucaoDeVagas.RegraDeDistribuicaoDeTipoInvalido(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao);
    }

    // Ausência é válida: a distribuição decide se a regra de ajuste é obrigatória conforme o ramo.
    private async Task<DomainError?> ResolverRegraDeAjusteAsync(CancellationToken cancellationToken)
    {
        if (pedido.RegraAjusteCodigo is null)
        {
            return null;
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(pedido.RegraAjusteCodigo, pedido.RegraAjusteVersao!, cancellationToken).ConfigureAwait(false);
        return ConferirRegraDeAjuste(regra, pedido.RegraAjusteCodigo);
    }

    private DomainError? ConferirRegraDeAjuste(RegraCatalogo? regra, string codigo)
    {
        if (regra is null)
        {
            return RecusasDaResolucaoDeVagas.RegraDeAjusteNaoEncontrada(codigo, pedido.RegraAjusteVersao);
        }

        _regraDeAjuste = regra.Referencia();
        return regra.Tipo == TipoRegra.RegraAjusteDistribuicaoVagas
            ? null
            : RecusasDaResolucaoDeVagas.RegraDeAjusteDeTipoInvalido(codigo, pedido.RegraAjusteVersao);
    }

    // O quadro que traz uma modalidade fora das selecionadas é reconciliação de ids crus: é da aplicação, e
    // não do domínio.
    private DomainError? ConferirQuadroDasModalidadesSelecionadas() =>
        pedido.Quadro
            .Where(linha => !pedido.ModalidadeIds.Contains(linha.ModalidadeId))
            .Select(linha => new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada",
                $"A modalidade {linha.ModalidadeId} no quadro não está entre as modalidades selecionadas desta oferta."))
            .FirstOrDefault();

    private async Task<DomainError?> ResolverReferenciaDemograficaAsync(CancellationToken cancellationToken)
    {
        if (pedido.ReferenciaReservaDemograficaId is not { } referenciaId)
        {
            return null;
        }

        ReferenciaReservaDemograficaView? view = await referenciaReservaDemograficaReader
            .ObterPorIdAsync(referenciaId, cancellationToken).ConfigureAwait(false);
        return view is null
            ? RecusasDaResolucaoDeVagas.ReferenciaDemograficaNaoEncontrada(referenciaId)
            : CongelarReferenciaDemografica(view);
    }

    private DomainError? CongelarReferenciaDemografica(ReferenciaReservaDemograficaView view)
    {
        Result<ReferenciaReservaDemograficaSnapshot> snapshot = ReferenciaReservaDemograficaSnapshot.Criar(
            view.Id, view.CensoReferencia, view.PpiPercentual, view.QuilombolaPercentual, view.PcdPercentual, view.BaseLegal);
        _demografica = snapshot.Value;
        return snapshot.IsFailure ? snapshot.Error : null;
    }

    private async Task<DomainError?> ResolverModalidadesAsync(CancellationToken cancellationToken)
    {
        foreach (Guid modalidadeId in pedido.ModalidadeIds)
        {
            Result<ModalidadeSelecionada> modalidade = await ResolverModalidadeAsync(modalidadeId, cancellationToken)
                .ConfigureAwait(false);
            if (modalidade.IsFailure)
            {
                return modalidade.Error;
            }

            _modalidades.Add(modalidade.Value!);
        }

        return null;
    }

    private async Task<Result<ModalidadeSelecionada>> ResolverModalidadeAsync(Guid modalidadeId, CancellationToken cancellationToken)
    {
        ModalidadeView? view = await modalidadeReader.ObterPorIdAsync(modalidadeId, cancellationToken).ConfigureAwait(false);
        return view is null
            ? Result<ModalidadeSelecionada>.Failure(RecusasDaResolucaoDeVagas.ModalidadeNaoEncontrada(modalidadeId))
            : ModalidadeSelecionada.Criar(
                view.Id, view.Codigo, view.Descricao,
                NaturezaLegalModalidadeCodigo.FromCodigo(view.NaturezaLegal), ComposicaoVagasModalidadeCodigo.FromCodigo(view.ComposicaoVagas), view.ComposicaoOrigem,
                RegraRemanejamentoModalidadeCodigo.FromCodigo(view.RegraRemanejamento), view.RemanejamentoDestino, view.RemanejamentoPar, view.RemanejamentoFallback,
                view.RegraDeAplicabilidade, view.AcaoQuandoIndeferido, view.BaseLegal ?? string.Empty,
                pedido.Quadro.FirstOrDefault(linha => linha.ModalidadeId == modalidadeId)?.Quantidade);
    }

    private DistribuicaoDeVagasDeclarada Declarada() => new()
    {
        RegraDeDistribuicao = _regra.Referencia(),
        VagasOfertadasBase = pedido.VagasOfertadasBase,
        PercentualDaReservaLegal = pedido.PercentualDaReservaLegal,
        ReferenciaDemografica = _demografica,
        RegraDeAjuste = _regraDeAjuste,
        AjusteDoQuadro = pedido.AjusteDoQuadro,
        Modalidades = _modalidades,
    };

    private FatosDaOfertaNoCadastro FatosDoCadastro() => new()
    {
        VagasAnuaisAutorizadas = _oferta.VagasAnuaisAutorizadas,
        ModalidadesAdmitidas = ModalidadesAdmitidasDoEsquemaArgs.Extrair(_regra),
        GrupoAreaEnem = _oferta.GrupoAreaEnem is { } grupo ? (grupo.Codigo, grupo.Rotulo) : null,
        OfertaCurso = _nomeDaOferta,
    };
}
