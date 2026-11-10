namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão de uma etapa da liberação da permissão reclassificada. A permissão da etapa é conferida antes da trava; a
/// regra vive no agregado <see cref="ReclassificacaoDaPermissao"/>, e aqui se traduz o pedido, se roda sob a ordem de
/// travas de <see cref="OperacaoSobreOPerfil"/> (o perfil e, em ordem de id, as concessões) e se grava de forma atômica o
/// que a decisão produziu. O conflito de concorrência segue a ADR-0119: o endpoint tem chave de idempotência e o captura.
/// </summary>
public sealed class DecisoesDaReclassificacao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly IReclassificacaoRepository _reclassificacoes;
    private readonly ConcessoesDaReclassificacao _concessoes;
    private readonly AutorizacaoDaReclassificacao _autorizacao;

    public DecisoesDaReclassificacao(
        OperacaoSobreOPerfil operacao,
        IReclassificacaoRepository reclassificacoes,
        ConcessoesDaReclassificacao concessoes,
        AutorizacaoDaReclassificacao autorizacao)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(autorizacao);

        _operacao = operacao;
        _reclassificacoes = reclassificacoes;
        _concessoes = concessoes;
        _autorizacao = autorizacao;
    }

    public async Task<GravacaoDaOperacao<DecisaoDaReclassificacaoDto>> DecidirAsync(
        Guid reclassificacaoId, DecidirReclassificacaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<Autorizada> autorizada = await AutorizarAsync(reclassificacaoId, dados, verificador, cancellationToken).ConfigureAwait(false);
        if (autorizada.IsFailure)
        {
            return Recusa(autorizada.Error!);
        }

        Autorizada decisor = autorizada.Value!;

        return await _operacao
            .ExecutarComAvisosAsync(
                decisor.PerfilId,
                (perfil, contexto) => NaTravaAsync(reclassificacaoId, perfil, new Decisao(decisor.Pedido, decisor.Autoria, contexto.Agora), cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<Autorizada>> AutorizarAsync(
        Guid reclassificacaoId, DecidirReclassificacaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        Result<PedidoLido> lido = Ler(dados);
        Result permitida = lido.IsFailure
            ? Result.Failure(lido.Error!)
            : await AutorizacaoDaReclassificacao.ConferirEtapaAsync(reclassificacaoId, lido.Value!.Etapa, verificador, cancellationToken).ConfigureAwait(false);

        return permitida.IsFailure
            ? Result<Autorizada>.Failure(permitida.Error!)
            : await ComAAutoriaAsync(reclassificacaoId, lido.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<Autorizada>> ComAAutoriaAsync(Guid reclassificacaoId, PedidoLido pedido, CancellationToken cancellationToken)
    {
        Guid? perfilId = await _reclassificacoes.ObterPerfilIdAsync(reclassificacaoId, cancellationToken).ConfigureAwait(false);
        if (perfilId is null)
        {
            return Result<Autorizada>.Failure(ReclassificacaoErros.NaoEncontrada());
        }

        Result<AutoriaDaVersao> autoria = await _autorizacao.AutoriaAsync(perfilId.Value, cancellationToken).ConfigureAwait(false);

        return autoria.IsFailure
            ? Result<Autorizada>.Failure(autoria.Error!)
            : Result<Autorizada>.Success(new Autorizada(perfilId.Value, pedido, autoria.Value!));
    }

    private static GravacaoDaOperacao<DecisaoDaReclassificacaoDto> Recusa(DomainError erro) =>
        GravacaoDaOperacao<DecisaoDaReclassificacaoDto>.Sem(Result<DecisaoDaReclassificacaoDto>.Failure(erro));

    private static Result<PedidoLido> Ler(DecidirReclassificacaoRequest dados)
    {
        EtapaDaVersao? etapa = EtapaDaVersaoCodigo.TentarLer(dados.Etapa);
        ResultadoDaLiberacao? resultado = ResultadoDaLiberacaoCodigo.TentarLer(dados.Resultado);
        bool alvoValido = dados.Todas ? dados.Concessoes is null or { Count: 0 } : dados.Concessoes is { Count: > 0 };

        DomainError? erro = etapa is null ? ReclassificacaoErros.EtapaInvalida()
            : resultado is null ? ReclassificacaoErros.ResultadoInvalido()
            : alvoValido ? null : ReclassificacaoErros.AlvoInvalido();

        return erro is not null
            ? Result<PedidoLido>.Failure(erro)
            : Result<PedidoLido>.Success(new PedidoLido(etapa!.Value, resultado!.Value, dados.Todas, dados.Concessoes ?? [], dados.Motivo));
    }

    private async Task<DesfechoDaOperacao<DecisaoDaReclassificacaoDto>> NaTravaAsync(
        Guid reclassificacaoId, Perfil perfil, Decisao decisao, CancellationToken cancellationToken)
    {
        ReclassificacaoDaPermissao? reclassificacao = await _reclassificacoes.ObterAsync(reclassificacaoId, cancellationToken).ConfigureAwait(false);
        if (reclassificacao is null)
        {
            return Falha(ReclassificacaoErros.NaoEncontrada());
        }

        IReadOnlyList<ConcessaoSuspensa> lidas = await _concessoes
            .LerComAPermissaoAsync(reclassificacao, decisao.Agora, true, cancellationToken).ConfigureAwait(false);
        PedidoDeDecisaoDaReclassificacao pedido = await MontarPedidoAsync(reclassificacao, perfil, lidas, decisao, cancellationToken).ConfigureAwait(false);
        Result<DesfechoDaDecisaoDaReclassificacao> desfecho = reclassificacao.Decidir(pedido);

        return desfecho.IsFailure ? Falha(desfecho.Error!) : Gravar(desfecho.Value!, pedido.LoteId);
    }

    private static DesfechoDaOperacao<DecisaoDaReclassificacaoDto> Falha(DomainError erro) =>
        new(Result<DecisaoDaReclassificacaoDto>.Failure(erro));

    private DesfechoDaOperacao<DecisaoDaReclassificacaoDto> Gravar(DesfechoDaDecisaoDaReclassificacao desfecho, Guid loteId)
    {
        foreach (LiberacaoDaReclassificacao liberacao in desfecho.Liberacoes)
        {
            _reclassificacoes.Adicionar(liberacao);
        }

        foreach (BaseDaPermissaoNaConcessao baseNova in desfecho.Bases)
        {
            _reclassificacoes.Adicionar(baseNova);
        }

        return new DesfechoDaOperacao<DecisaoDaReclassificacaoDto>(Result<DecisaoDaReclassificacaoDto>.Success(new DecisaoDaReclassificacaoDto(
            loteId,
            [.. desfecho.Liberacoes.Select(static l => l.ConcessaoId)],
            [.. desfecho.Bases.Select(static b => b.ConcessaoId)],
            desfecho.ParaOutraPessoa,
            desfecho.PuladasPorEscopo)));
    }

    private async Task<PedidoDeDecisaoDaReclassificacao> MontarPedidoAsync(
        ReclassificacaoDaPermissao reclassificacao,
        Perfil perfil,
        IReadOnlyList<ConcessaoSuspensa> lidas,
        Decisao decisao,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConcessaoNaReclassificacao> concessoes = await ConcessoesDoPedidoAsync(
            reclassificacao, perfil, lidas, decisao, cancellationToken).ConfigureAwait(false);

        return new PedidoDeDecisaoDaReclassificacao(
            decisao.Pedido.Etapa,
            decisao.Pedido.Resultado,
            decisao.Pedido.Motivo,
            decisao.Autoria.Pessoa,
            decisao.Autoria.TemConcessao,
            decisao.Pedido.Todas,
            Guid.CreateVersion7(),
            decisao.Agora,
            await _reclassificacoes.EhMaisRecenteAsync(reclassificacao, cancellationToken).ConfigureAwait(false),
            concessoes);
    }

    private async Task<IReadOnlyList<ConcessaoNaReclassificacao>> ConcessoesDoPedidoAsync(
        ReclassificacaoDaPermissao reclassificacao,
        Perfil perfil,
        IReadOnlyList<ConcessaoSuspensa> lidas,
        Decisao decisao,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConcessaoSuspensa> alvos = AlvosDa(reclassificacao, lidas, decisao.Pedido);
        IReadOnlySet<Guid> versoesLidas = await _reclassificacoes
            .ListarVersoesComDocumentoLidoAsync(
                decisao.Autoria.Pessoa,
                [.. perfil.Versoes.Where(v => alvos.Any(a => a.Item.Concessao.VersaoId == v.Id))],
                cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<Guid> ausentes = decisao.Pedido.Concessoes.Where(id => !lidas.Any(c => c.Item.Concessao.Id == id));

        return [.. alvos.Select(a => ParaOPedido(a, versoesLidas)), .. ausentes.Select(Ausente)];
    }

    private static List<ConcessaoSuspensa> AlvosDa(
        ReclassificacaoDaPermissao reclassificacao, IReadOnlyList<ConcessaoSuspensa> lidas, PedidoLido pedido) =>
        pedido.Todas
            ? [.. lidas.Where(c => reclassificacao.SuspendeA(c.Item.Base) && c.Aguarda(reclassificacao, pedido.Etapa))]
            : [.. lidas.Where(c => pedido.Concessoes.Contains(c.Item.Concessao.Id))];

    // A concessão pedida que não reúne a permissão (revogada, de outro perfil ou inexistente) não está suspensa por esta
    // reclassificação, e a conferência do agregado a recusa.
    private static ConcessaoNaReclassificacao Ausente(Guid id) =>
        new(id, new Identidade("ausente", "ausente"), false, new BaseDaConcessao(ClassificacaoDeProtecao.Nenhuma, false), TipoDeEscopo.Instituicao, [], true);

    private static ConcessaoNaReclassificacao ParaOPedido(ConcessaoSuspensa suspensa, IReadOnlySet<Guid> versoesLidas)
    {
        Concessao concessao = suspensa.Item.Concessao;

        return new ConcessaoNaReclassificacao(
            concessao.Id,
            concessao.Beneficiario,
            true,
            suspensa.Item.Base,
            concessao.EscopoTipo,
            suspensa.Decisoes,
            concessao.VersaoId is { } versaoId && versoesLidas.Contains(versaoId));
    }

    private sealed record PedidoLido(EtapaDaVersao Etapa, ResultadoDaLiberacao Resultado, bool Todas, IReadOnlyList<Guid> Concessoes, string? Motivo);

    private sealed record Autorizada(Guid PerfilId, PedidoLido Pedido, AutoriaDaVersao Autoria);

    private sealed record Decisao(PedidoLido Pedido, AutoriaDaVersao Autoria, DateTimeOffset Agora);
}
