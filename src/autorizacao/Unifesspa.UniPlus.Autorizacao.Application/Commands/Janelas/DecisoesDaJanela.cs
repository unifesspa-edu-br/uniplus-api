namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As decisões sobre as concessões da janela de migração: o lote da gestão, a retida, a etapa da proteção de dados e o
/// encerramento manual. A permissão é conferida aqui, antes da trava; a regra vive na janela, e aqui se traduz o pedido,
/// se roda sob as travas de <see cref="MigracaoSobreOPerfil"/> e se devolve a janela com os avisos da gravação.
/// </summary>
public sealed class DecisoesDaJanela
{
    private readonly MigracaoSobreOPerfil _migracao;
    private readonly AutorizacaoDaMigracao _autorizacao;
    private readonly AvisosDaJanela _avisos;
    private readonly AvisosDosEfeitosDaJanela _efeitos;

    public DecisoesDaJanela(
        MigracaoSobreOPerfil migracao, AutorizacaoDaMigracao autorizacao, AvisosDaJanela avisos, AvisosDosEfeitosDaJanela efeitos)
    {
        ArgumentNullException.ThrowIfNull(migracao);
        ArgumentNullException.ThrowIfNull(autorizacao);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(efeitos);

        _migracao = migracao;
        _autorizacao = autorizacao;
        _avisos = avisos;
        _efeitos = efeitos;
    }

    public Task<GravacaoDaOperacao<JanelaDeMigracaoDto>> EnviarLoteAsync(
        Guid perfilId, EnviarLoteDaMigracaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDoLote pedido = new(Itens(dados.Decisoes), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            new Etapa(EtapaDaVersao.GestaoDeAcessos, TravaProtecao: true),
            verificador,
            (j, autoria) => j.Janela.EnviarLote(pedido, autoria, j.Contexto),
            OperacaoNaJanela.Lote,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<JanelaDeMigracaoDto>> DecidirRetidaAsync(
        Guid perfilId, Guid concessaoId, DecidirRetidaRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDaRetida pedido = new(concessaoId, ResultadoDaMigracaoCodigo.TentarLerDaPessoa(dados.Decisao), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            new Etapa(EtapaDaVersao.GestaoDeAcessos, TravaProtecao: true),
            verificador,
            (j, autoria) => j.Janela.DecidirRetida(pedido, autoria, j.Contexto),
            OperacaoNaJanela.Retida,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<JanelaDeMigracaoDto>> DecidirEtapaDaProtecaoAsync(
        Guid perfilId, DecidirEtapaDaProtecaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDaEtapaDaProtecao pedido = new(Itens(dados.Decisoes), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            new Etapa(EtapaDaVersao.ProtecaoDeDados, TravaProtecao: false),
            verificador,
            (j, autoria) => j.Janela.DecidirEtapaDaProtecao(pedido, autoria, j.Contexto),
            OperacaoNaJanela.EtapaDaProtecao,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<JanelaDeMigracaoDto>> EncerrarAsync(
        Guid perfilId, EncerrarJanelaRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return ExecutarAsync(
            perfilId,
            new Etapa(EtapaDaVersao.GestaoDeAcessos, TravaProtecao: false),
            verificador,
            (j, autoria) => j.Janela.EncerrarManualmente(new EncerramentoManual(autoria, dados.Motivo), dados.ConfirmacaoDeExclusao, j.Contexto),
            OperacaoNaJanela.Encerramento,
            cancellationToken);
    }

    private static List<ItemDoLote> Itens(IReadOnlyList<DecisaoDaMigracaoRequest>? decisoes) =>
        [.. (decisoes ?? []).Select(static d => new ItemDoLote(d.ConcessaoId, ResultadoDaMigracaoCodigo.TentarLerDaPessoa(d.Decisao)))];

    // A decisão avisa quem migrou, quem foi recusado e quem decide o que vem depois; o lote retém as concessões do gestor
    // que o enviou, e quem pode decidi-las precisa saber que o prazo corre; o encerramento da janela avisa o fim.
    private async Task<IReadOnlyList<EnviarEmail>> AvisosAsync(
        JanelaDaOperacao janela, EstadoDaJanelaAntesDaOperacao antes, OperacaoNaJanela operacao, Identidade ator, CancellationToken cancellationToken)
    {
        MudancasDaJanela mudancas = MudancasDaJanela.Entre(antes, janela.Janela, janela.Contexto);
        IReadOnlyList<EnviarEmail> dasDecisoes = await _efeitos.DaDecisaoAsync(janela, mudancas, operacao, ator, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> doFim = await _efeitos.DoFimAsync(janela, mudancas, ator, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dasRetidas = operacao == OperacaoNaJanela.Lote
            ? await AvisarDasRetidasAsync(janela, cancellationToken).ConfigureAwait(false)
            : [];

        return [.. dasDecisoes, .. doFim, .. dasRetidas];
    }

    private Task<IReadOnlyList<EnviarEmail>> AvisarDasRetidasAsync(JanelaDaOperacao janela, CancellationToken cancellationToken)
    {
        IReadOnlyList<Concessao> retidas =
        [
            .. janela.Contexto.Concessoes.Where(c => janela.Janela.LinhaDe(c.Id)?.Situacao == SituacaoDaMigracao.Retida),
        ];

        return retidas.Count == 0
            ? Task.FromResult<IReadOnlyList<EnviarEmail>>([])
            : _avisos.DasRetidasAsync(janela.Perfil, janela.Janela, retidas, janela.Contexto.Agora, cancellationToken);
    }

    private async Task<GravacaoDaOperacao<JanelaDeMigracaoDto>> ExecutarAsync(
        Guid perfilId,
        Etapa etapa,
        IVerificadorDeAcesso verificador,
        Func<JanelaDaOperacao, AutoriaDaVersao, Result> transicao,
        OperacaoNaJanela operacao,
        CancellationToken cancellationToken)
    {
        Result<AutoriaDaVersao> autorizada = await _autorizacao
            .AutorizarAsync(perfilId, etapa.Permissao, verificador, cancellationToken)
            .ConfigureAwait(false);
        if (autorizada.IsFailure)
        {
            return GravacaoDaOperacao<JanelaDeMigracaoDto>.Sem(Result<JanelaDeMigracaoDto>.Failure(autorizada.Error!));
        }

        AutoriaDaVersao autoria = autorizada.Value!;

        return await _migracao
            .ExecutarAsync(
                perfilId,
                etapa.TravaProtecao,
                j => DecidirAsync(j, autoria, transicao, operacao, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DesfechoDaOperacao<JanelaDeMigracaoDto>> DecidirAsync(
        JanelaDaOperacao janela,
        AutoriaDaVersao autoria,
        Func<JanelaDaOperacao, AutoriaDaVersao, Result> transicao,
        OperacaoNaJanela operacao,
        CancellationToken cancellationToken)
    {
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela.Janela, janela.Contexto);
        Result resultado = transicao(janela, autoria);
        if (resultado.IsFailure)
        {
            return new DesfechoDaOperacao<JanelaDeMigracaoDto>(Result<JanelaDeMigracaoDto>.ValidationFailure(resultado.Errors));
        }

        JanelaDeMigracaoDto dto = JanelaMapping.ToDto(janela.Janela, janela.Perfil, janela.Contexto, autoria.Pessoa);
        IReadOnlyList<EnviarEmail> avisos = await AvisosAsync(janela, antes, operacao, autoria.Pessoa, cancellationToken).ConfigureAwait(false);

        return new DesfechoDaOperacao<JanelaDeMigracaoDto>(Result<JanelaDeMigracaoDto>.Success(dto), Avisos: avisos);
    }

    private sealed record Etapa(EtapaDaVersao Permissao, bool TravaProtecao);
}
