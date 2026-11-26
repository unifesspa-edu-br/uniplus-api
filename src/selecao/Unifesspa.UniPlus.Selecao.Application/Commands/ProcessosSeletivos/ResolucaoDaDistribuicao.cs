namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Vagas;
using Domain.ValueObjects;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;

/// <summary>
/// A resolução de uma distribuição de vagas pedida (ids e códigos crus do payload) contra os cadastros vivos
/// e o catálogo de regras. Todas as resoluções rodam, e cada recusa fica guardada com o campo do pedido: a
/// oferta, a regra de distribuição, a regra de ajuste, a referência demográfica e cada modalidade. O dado
/// recusado fica fora da declaração e consta como recusado, para que a conferência do domínio só deixe de rodar
/// a regra que o lê. O que resolveu fica guardado para a criação da configuração, que congela cada peça por
/// valor (ADR-0061).
/// </summary>
internal sealed class ResolucaoDaDistribuicao(
    ConfiguracaoDistribuicaoVagasInput pedido,
    IRegraCatalogoReader regraCatalogoReader,
    IOfertaCursoReader ofertaCursoReader,
    IModalidadeReader modalidadeReader,
    IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader)
{
    private OfertaCursoView? _oferta;
    private CopiaDaOfertaDeCurso? _nomeDaOferta;
    private RegraCatalogo? _regra;
    private ReferenciaRegra? _regraDeAjuste;
    private ReferenciaReservaDemograficaSnapshot? _demografica;
    private readonly List<ModalidadeSelecionada> _modalidades = [];
    private readonly List<FieldError> _recusasDoCadastro = [];
    private readonly HashSet<DadoDaDistribuicaoDeVagas> _recusados = [];

    /// <summary>A forma do pedido, que não depende de nenhum cadastro.</summary>
    internal IReadOnlyList<FieldError> RecusasDaForma { get; private set; } = [];

    /// <summary>O que os cadastros e o catálogo de regras recusaram do pedido.</summary>
    internal IReadOnlyList<FieldError> RecusasDoCadastro => _recusasDoCadastro;

    /// <summary>O que a declaração do pedido, com os dados aceitos, não cumpre.</summary>
    internal IReadOnlyList<FieldError> RecusasDaDeclaracao { get; private set; } = [];

    internal bool SemRecusas =>
        RecusasDaForma.Count == 0 && RecusasDoCadastro.Count == 0 && RecusasDaDeclaracao.Count == 0;

    internal async Task ResolverAsync(CancellationToken cancellationToken)
    {
        await ResolverOfertaAsync(cancellationToken).ConfigureAwait(false);
        Recusar(DadoDaDistribuicaoDeVagas.RegraDeDistribuicao, "regraDistribuicaoCodigo", await ResolverRegraDeDistribuicaoAsync(cancellationToken).ConfigureAwait(false));
        Recusar(DadoDaDistribuicaoDeVagas.RegraDeAjuste, "regraAjusteCodigo", await ResolverRegraDeAjusteAsync(cancellationToken).ConfigureAwait(false));
        Recusar(DadoDaDistribuicaoDeVagas.ReferenciaDemografica, "referenciaReservaDemograficaId", await ResolverReferenciaDemograficaAsync(cancellationToken).ConfigureAwait(false));
        await ResolverModalidadesAsync(cancellationToken).ConfigureAwait(false);

        RecusasDaForma = ConferirForma();
        RecusasDaDeclaracao = ConfiguracaoDistribuicaoVagas.ConferirDeclaracao(Declarada(), FatosDoCadastro());
    }

    /// <summary>Cria a configuração, que monta o quadro: só quando a oferta não tem recusa alguma.</summary>
    internal Result<ConfiguracaoDistribuicaoVagas> Montar() =>
        ConfiguracaoDistribuicaoVagas.Criar(pedido.OfertaCursoId, Declarada(), FatosDoCadastro());

    private void Recusar(string campo, DomainError? erro)
    {
        if (erro is not null)
        {
            _recusasDoCadastro.Add(new FieldError(campo, erro));
        }
    }

    private void Recusar(DadoDaDistribuicaoDeVagas dado, string campo, DomainError? erro)
    {
        if (erro is not null)
        {
            _recusados.Add(dado);
        }

        Recusar(campo, erro);
    }

    private async Task ResolverOfertaAsync(CancellationToken cancellationToken)
    {
        OfertaCursoView? oferta = await ofertaCursoReader
            .ObterPorIdAsync(pedido.OfertaCursoId, cancellationToken).ConfigureAwait(false);
        if (oferta is null)
        {
            Recusar("ofertaCursoId", RecusasDaResolucaoDeVagas.OfertaNaoEncontrada(pedido.OfertaCursoId));
            return;
        }

        Result<CopiaDaOfertaDeCurso> nome = CopiaDaOfertaDeCurso.Criar(
            oferta.Codigo, oferta.CursoNome, oferta.CursoGrau, oferta.UnidadeOfertanteNome, oferta.Turnos);
        _oferta = oferta;
        _nomeDaOferta = nome.Value;
        _recusasDoCadastro.AddRange(nome.Errors.Select(static causa => new FieldError("ofertaCursoId", causa.Error)));
    }

    private async Task<DomainError?> ResolverRegraDeDistribuicaoAsync(CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = ConferirRegraDeDistribuicao(regra);
        _regra = recusa is null ? regra : null;
        return recusa;
    }

    private DomainError? ConferirRegraDeDistribuicao(RegraCatalogo? regra) =>
        regra is null
            ? RecusasDaResolucaoDeVagas.RegraDeDistribuicaoNaoEncontrada(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao)
            : regra.Tipo == TipoRegra.RegraDistribuicaoVagas
                ? null
                : RecusasDaResolucaoDeVagas.RegraDeDistribuicaoDeTipoInvalido(pedido.RegraDistribuicaoCodigo, pedido.RegraDistribuicaoVersao);

    // Ausência é válida: a distribuição decide se a regra de ajuste é obrigatória conforme o tipo de distribuição.
    private async Task<DomainError?> ResolverRegraDeAjusteAsync(CancellationToken cancellationToken)
    {
        if (pedido.RegraAjusteCodigo is null)
        {
            return null;
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(pedido.RegraAjusteCodigo, pedido.RegraAjusteVersao!, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = ConferirRegraDeAjuste(regra, pedido.RegraAjusteCodigo);
        _regraDeAjuste = recusa is null ? regra!.Referencia() : null;
        return recusa;
    }

    private DomainError? ConferirRegraDeAjuste(RegraCatalogo? regra, string codigo) =>
        regra is null
            ? RecusasDaResolucaoDeVagas.RegraDeAjusteNaoEncontrada(codigo, pedido.RegraAjusteVersao)
            : regra.Tipo == TipoRegra.RegraAjusteDistribuicaoVagas
                ? null
                : RecusasDaResolucaoDeVagas.RegraDeAjusteDeTipoInvalido(codigo, pedido.RegraAjusteVersao);

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
        return snapshot.Error;
    }

    private async Task ResolverModalidadesAsync(CancellationToken cancellationToken)
    {
        for (int indice = 0; indice < pedido.ModalidadeIds.Count; indice++)
        {
            Result<ModalidadeSelecionada> modalidade = await ResolverModalidadeAsync(pedido.ModalidadeIds[indice], cancellationToken)
                .ConfigureAwait(false);
            RecusarAModalidade(indice, modalidade.Errors);
            if (modalidade.IsSuccess)
            {
                _modalidades.Add(modalidade.Value!);
            }
        }
    }

    private void RecusarAModalidade(int indice, IReadOnlyList<FieldError> causas)
    {
        if (causas.Count > 0)
        {
            _recusados.Add(DadoDaDistribuicaoDeVagas.Modalidades);
        }

        _recusasDoCadastro.AddRange(causas.Select(c => c.NoCaminhoDoItem($"modalidadeIds[{indice}]")));
    }

    private async Task<Result<ModalidadeSelecionada>> ResolverModalidadeAsync(Guid modalidadeId, CancellationToken cancellationToken)
    {
        ModalidadeView? view = await modalidadeReader.ObterPorIdAsync(modalidadeId, cancellationToken).ConfigureAwait(false);
        return view is null
            ? Result<ModalidadeSelecionada>.Failure(RecusasDaResolucaoDeVagas.ModalidadeNaoEncontrada(modalidadeId))
            : ModalidadeSelecionada.Criar(NoCadastro(view), pedido.Quadro.FirstOrDefault(linha => linha.ModalidadeId == modalidadeId)?.Quantidade);
    }

    private static ModalidadeNoCadastro NoCadastro(ModalidadeView view) => new()
    {
        Id = view.Id,
        Codigo = view.Codigo,
        Descricao = view.Descricao,
        NaturezaLegal = CodigoReconhecido<NaturezaLegalModalidade>(view.NaturezaLegal),
        ComposicaoVagas = CodigoReconhecido<ComposicaoVagasModalidade>(view.ComposicaoVagas),
        ComposicaoOrigemCodigo = view.ComposicaoOrigem,
        RegraRemanejamento = view.RegraRemanejamento is { } regra ? CatalogoDe<RegraRemanejamentoModalidade>.Obter(regra) : null,
        RemanejamentoDestino = view.RemanejamentoDestino,
        RemanejamentoPar = view.RemanejamentoPar,
        RemanejamentoFallback = view.RemanejamentoFallback,
        RegraDeAplicabilidade = view.RegraDeAplicabilidade,
        AcaoQuandoIndeferido = view.AcaoQuandoIndeferido,
        BaseLegal = view.BaseLegal ?? string.Empty,
    };

    private static TEnum? CodigoReconhecido<TEnum>(string codigo) where TEnum : struct, Enum =>
        CatalogoDe<TEnum>.TentarObter(codigo, out TEnum valor) ? valor : null;

    // A forma do pedido é do domínio; o quadro que traz uma modalidade fora das selecionadas é reconciliação
    // de ids crus, e é da aplicação.
    private IReadOnlyList<FieldError> ConferirForma() =>
    [
        .. ConferenciaDaDistribuicaoDeVagas.ConferirForma(
            pedido.VagasOfertadasBase,
            pedido.PercentualDaReservaLegal,
            pedido.ModalidadeIds.Count,
            [.. pedido.Quadro.Select(static linha => (int?)linha.Quantidade)]),
        .. pedido.Quadro
            .Select((linha, indice) => (linha, indice))
            .Where(par => !pedido.ModalidadeIds.Contains(par.linha.ModalidadeId))
            .Select(par => new FieldError(
                $"quadro[{par.indice}].modalidadeId",
                RecusasDaResolucaoDeVagas.QuadroModalidadeNaoSelecionada(par.linha.ModalidadeId))),
    ];

    private DistribuicaoDeVagasDeclarada Declarada() => new()
    {
        RegraDeDistribuicao = _regra?.Referencia(),
        VagasOfertadasBase = pedido.VagasOfertadasBase,
        PercentualDaReservaLegal = pedido.PercentualDaReservaLegal,
        ReferenciaDemografica = _demografica,
        RegraDeAjuste = _regraDeAjuste,
        AjusteDoQuadro = pedido.AjusteDoQuadro,
        Modalidades = _modalidades,
        Recusados = _recusados,
    };

    // A oferta não encontrada não dá teto nem grupo de área do ENEM, e a regra recusada não dá rol.
    private FatosDaOfertaNoCadastro FatosDoCadastro() => new()
    {
        VagasAnuaisAutorizadas = _oferta?.VagasAnuaisAutorizadas,
        ModalidadesAdmitidas = _regra is null ? null : ModalidadesAdmitidasDaDeclaracaoDaRegra.Extrair(_regra),
        GrupoAreaEnem = _oferta?.GrupoAreaEnem is { } grupo ? (grupo.Codigo, grupo.Rotulo) : null,
        OfertaCurso = _nomeDaOferta,
    };
}
