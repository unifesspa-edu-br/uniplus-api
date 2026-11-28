namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.LeituraDoDocumento;
using Domain.ProcessosSeletivos.Vagas;
using Domain.ValueObjects;

using Kernel.Results;

using Services;
using Services.DocumentoDaConfiguracao;

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
    private IReadOnlyDictionary<Guid, string?> _citacoesDoQuadroForaDaSelecao = new Dictionary<Guid, string?>();

    // O quadro ausente é recusado pela forma do pedido; as conferências que dependem dele deixam de rodar.
    private IReadOnlyList<QuantidadeVagaInput> Quadro => pedido.Quadro ?? [];

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

        _citacoesDoQuadroForaDaSelecao = await CitarAsModalidadesDoQuadroForaDaSelecaoAsync(cancellationToken).ConfigureAwait(false);
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
            Recusar("ofertaCursoId", RecusasDeCadastroDasVagas.OfertaNaoEncontrada());
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
            .ObterAsync(pedido.RegraDistribuicaoCodigo!, pedido.RegraDistribuicaoVersao!, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = ConferirRegraDeDistribuicao(regra);
        _regra = recusa is null ? regra : null;
        return recusa;
    }

    private static DomainError? ConferirRegraDeDistribuicao(RegraCatalogo? regra) =>
        regra is null
            ? RecusasDaResolucaoDeVagas.RegraDeDistribuicaoNaoEncontrada()
            : regra.Tipo == TipoRegra.RegraDistribuicaoVagas
                ? null
                : RecusasDaResolucaoDeVagas.RegraDeDistribuicaoDeTipoInvalido();

    // Ausência é válida: a distribuição decide se a regra de ajuste é obrigatória conforme o tipo de distribuição.
    private async Task<DomainError?> ResolverRegraDeAjusteAsync(CancellationToken cancellationToken)
    {
        if (pedido.RegraAjusteCodigo is null)
        {
            return null;
        }

        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(pedido.RegraAjusteCodigo, pedido.RegraAjusteVersao!, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = ConferirRegraDeAjuste(regra);
        _regraDeAjuste = recusa is null ? regra!.Referencia() : null;
        return recusa;
    }

    private static DomainError? ConferirRegraDeAjuste(RegraCatalogo? regra) =>
        regra is null
            ? RecusasDaResolucaoDeVagas.RegraDeAjusteNaoEncontrada()
            : regra.Tipo == TipoRegra.RegraAjusteDistribuicaoVagas
                ? null
                : RecusasDaResolucaoDeVagas.RegraDeAjusteDeTipoInvalido();

    private async Task<DomainError?> ResolverReferenciaDemograficaAsync(CancellationToken cancellationToken)
    {
        if (pedido.ReferenciaReservaDemograficaId is not { } referenciaId)
        {
            return null;
        }

        ReferenciaReservaDemograficaView? view = await referenciaReservaDemograficaReader
            .ObterPorIdAsync(referenciaId, cancellationToken).ConfigureAwait(false);
        return view is null
            ? RecusasDeCadastroDasVagas.ReferenciaDemograficaNaoEncontrada()
            : CongelarReferenciaDemografica(view);
    }

    private DomainError? CongelarReferenciaDemografica(ReferenciaReservaDemograficaView view)
    {
        Result<ReferenciaReservaDemograficaSnapshot> snapshot = CopiaDosCadastrosDasVagas.ReferenciaDemografica(view);
        _demografica = snapshot.Value;
        return snapshot.Error;
    }

    // A lista ausente é recusada pela forma do pedido, e as regras que leem as modalidades deixam de rodar.
    private async Task ResolverModalidadesAsync(CancellationToken cancellationToken)
    {
        if (pedido.ModalidadeIds is { } modalidadeIds)
        {
            await ResolverCadaModalidadeAsync(modalidadeIds, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _recusados.Add(DadoDaDistribuicaoDeVagas.Modalidades);
        }
    }

    private async Task ResolverCadaModalidadeAsync(IReadOnlyList<Guid> modalidadeIds, CancellationToken cancellationToken)
    {
        for (int indice = 0; indice < modalidadeIds.Count; indice++)
        {
            Result<ModalidadeSelecionada> modalidade = await ResolverModalidadeAsync(modalidadeIds[indice], cancellationToken)
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
            ? Result<ModalidadeSelecionada>.Failure(RecusasDeCadastroDasVagas.ModalidadeNaoEncontrada())
            : ModalidadeSelecionada.Criar(CopiaDosCadastrosDasVagas.Modalidade(view), Quadro.FirstOrDefault(linha => linha.ModalidadeId == modalidadeId)?.Quantidade);
    }

    // A recusa cita a modalidade pelo código do cadastro, seguido da descrição quando há; só as linhas fora da
    // seleção são consultadas, e sem registro a recusa não cita a modalidade.
    private async Task<IReadOnlyDictionary<Guid, string?>> CitarAsModalidadesDoQuadroForaDaSelecaoAsync(CancellationToken cancellationToken)
    {
        Dictionary<Guid, string?> citacoes = [];
        foreach (Guid id in Quadro.Select(static linha => linha.ModalidadeId).Except(pedido.ModalidadeIds ?? []))
        {
            ModalidadeView? view = await modalidadeReader.ObterPorIdAsync(id, cancellationToken).ConfigureAwait(false);
            citacoes[id] = view is null ? null : CitacaoDaModalidade(view);
        }

        return citacoes;
    }

    private static string CitacaoDaModalidade(ModalidadeView view) =>
        string.IsNullOrWhiteSpace(view.Descricao) ? view.Codigo : $"{view.Codigo} — {view.Descricao}";

    // A forma do pedido é do domínio; o quadro que traz uma modalidade fora das selecionadas é reconciliação
    // de ids crus, e é da aplicação.
    private IReadOnlyList<FieldError> ConferirForma() =>
    [
        .. ConferenciaDaDistribuicaoDeVagas.ConferirFormaDoPedido(
            pedido.VagasOfertadasBase,
            pedido.PercentualDaReservaLegal,
            pedido.ModalidadeIds?.Count,
            pedido.Quadro is null ? null : [.. pedido.Quadro.Select(static linha => linha.Quantidade)]),
        .. Quadro
            .Select((linha, indice) => (linha, indice))
            .Where(par => pedido.ModalidadeIds is not null && !pedido.ModalidadeIds.Contains(par.linha.ModalidadeId))
            .Select(par => new FieldError(
                $"quadro[{par.indice}].modalidadeId",
                RecusasDaResolucaoDeVagas.QuadroModalidadeNaoSelecionada(_citacoesDoQuadroForaDaSelecao.GetValueOrDefault(par.linha.ModalidadeId)))),
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
        ModalidadesComQuantidadeRecusada = ModalidadesSemQuantidadeConferivel(),
    };

    // Sem o quadro nenhuma quantidade é conhecida: a regra que exige ou veda a quantidade fixada deixa de rodar para todas.
    private HashSet<Guid> ModalidadesSemQuantidadeConferivel() =>
        pedido.Quadro is null
            ? [.. pedido.ModalidadeIds ?? []]
            : [.. Quadro.Where(static linha => linha.Quantidade is null).Select(static linha => linha.ModalidadeId)];

    // A oferta não encontrada não dá teto nem grupo de área do ENEM, e a regra recusada não dá rol.
    private FatosDaOfertaNoCadastro FatosDoCadastro() => new()
    {
        VagasAnuaisAutorizadas = _oferta?.VagasAnuaisAutorizadas,
        ModalidadesAdmitidas = _regra is null ? null : ModalidadesAdmitidasDaDeclaracaoDaRegra.Extrair(_regra.DeclaracaoDaRegra),
        GrupoAreaEnem = _oferta?.GrupoAreaEnem is { } grupo ? (grupo.Codigo, grupo.Rotulo) : null,
        OfertaCurso = _nomeDaOferta,
    };
}
