namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As decisões sobre as concessões da revisão das concessões: o envio de decisões da gestão, a retida, a etapa de proteção de dados e o
/// encerramento manual. A permissão é conferida aqui, antes do bloqueio; a regra vive na revisão, e aqui se traduz o pedido,
/// se roda sob os bloqueios de <see cref="MigracaoSobreOPerfil"/> e se devolve a revisão com os avisos da gravação.
/// </summary>
public sealed class DecisoesDaRevisao
{
    private readonly MigracaoSobreOPerfil _migracao;
    private readonly AutorizacaoDaMigracao _autorizacao;
    private readonly AvisosDaRevisao _avisos;
    private readonly AvisosDosEfeitosDaRevisao _efeitos;

    public DecisoesDaRevisao(
        MigracaoSobreOPerfil migracao, AutorizacaoDaMigracao autorizacao, AvisosDaRevisao avisos, AvisosDosEfeitosDaRevisao efeitos)
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

    public Task<GravacaoDaOperacao<RevisaoDasConcessoesDto>> EnviarDecisoesDaGestaoAsync(
        Guid perfilId, EnviarDecisoesDaRevisaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDoEnvio pedido = new(Itens(dados.Decisoes), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            EtapaDaVersao.GestaoDeAcessos,
            verificador,
            (j, autoria) => j.Revisao.EnviarDecisoesDaGestao(pedido, autoria, j.Contexto),
            OperacaoNaRevisaoDasConcessoes.EnvioDaGestao,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<RevisaoDasConcessoesDto>> DecidirRetidaAsync(
        Guid perfilId, Guid concessaoId, DecidirRetidaRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDaRetida pedido = new(concessaoId, LerDecisaoDaPessoa(dados.Decisao), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            EtapaDaVersao.GestaoDeAcessos,
            verificador,
            (j, autoria) => j.Revisao.DecidirRetida(pedido, autoria, j.Contexto),
            OperacaoNaRevisaoDasConcessoes.Retida,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<RevisaoDasConcessoesDto>> DecidirEtapaDaProtecaoAsync(
        Guid perfilId, DecidirEtapaDaProtecaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        PedidoDaEtapaDaProtecao pedido = new(Itens(dados.Decisoes), dados.ConfirmacaoDeExclusao);

        return ExecutarAsync(
            perfilId,
            EtapaDaVersao.ProtecaoDeDados,
            verificador,
            (j, autoria) => j.Revisao.DecidirEtapaDaProtecao(pedido, autoria, j.Contexto),
            OperacaoNaRevisaoDasConcessoes.EtapaDaProtecao,
            cancellationToken);
    }

    public Task<GravacaoDaOperacao<RevisaoDasConcessoesDto>> EncerrarAsync(
        Guid perfilId, EncerrarRevisaoRequest dados, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return ExecutarAsync(
            perfilId,
            EtapaDaVersao.GestaoDeAcessos,
            verificador,
            (j, autoria) => j.Revisao.EncerrarManualmente(new EncerramentoManual(autoria, dados.Motivo), dados.ConfirmacaoDeExclusao, j.Contexto),
            OperacaoNaRevisaoDasConcessoes.Encerramento,
            cancellationToken);
    }

    private static List<ItemDoEnvio> Itens(IReadOnlyList<DecisaoDaMigracaoRequest>? decisoes) =>
        [.. (decisoes ?? []).Select(static d => new ItemDoEnvio(d.ConcessaoId, LerDecisaoDaPessoa(d.Decisao)))];

    // A decisão avisa quem migrou, quem foi recusado e quem decide o que vem depois; o envio de decisões retém as concessões do gestor
    // que o enviou, e quem pode decidi-las precisa saber que o prazo corre; o encerramento da revisão avisa o fim.
    private async Task<IReadOnlyList<EnviarEmail>> AvisosAsync(
        RevisaoDaOperacao revisao, EstadoDaRevisaoAntesDaOperacao antes, OperacaoNaRevisaoDasConcessoes operacao, Identidade ator, CancellationToken cancellationToken)
    {
        MudancasDaRevisaoDasConcessoes mudancas = MudancasDaRevisaoDasConcessoes.Entre(antes, revisao.Revisao, revisao.Contexto);
        IReadOnlyList<EnviarEmail> dasDecisoes = await _efeitos.DaDecisaoAsync(revisao, mudancas, operacao, ator, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> doFim = await _efeitos.DoFimAsync(revisao, mudancas, ator, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dasRetidas = operacao == OperacaoNaRevisaoDasConcessoes.EnvioDaGestao
            ? await AvisarDasRetidasAsync(revisao, cancellationToken).ConfigureAwait(false)
            : [];

        return [.. dasDecisoes, .. doFim, .. dasRetidas];
    }

    private Task<IReadOnlyList<EnviarEmail>> AvisarDasRetidasAsync(RevisaoDaOperacao revisao, CancellationToken cancellationToken)
    {
        IReadOnlyList<Concessao> retidas =
        [
            .. revisao.Contexto.Concessoes.Where(c => revisao.Revisao.LinhaDe(c.Id)?.Situacao == SituacaoDaMigracao.Retida),
        ];

        return retidas.Count == 0
            ? Task.FromResult<IReadOnlyList<EnviarEmail>>([])
            : _avisos.DasRetidasAsync(revisao.Perfil, revisao.Revisao, retidas, revisao.Contexto.Agora, cancellationToken);
    }

    private async Task<GravacaoDaOperacao<RevisaoDasConcessoesDto>> ExecutarAsync(
        Guid perfilId,
        EtapaDaVersao etapa,
        IVerificadorDeAcesso verificador,
        Func<RevisaoDaOperacao, AutoriaDaVersao, Result> transicao,
        OperacaoNaRevisaoDasConcessoes operacao,
        CancellationToken cancellationToken)
    {
        Result<AutoriaDaVersao> autorizada = await _autorizacao
            .AutorizarAsync(perfilId, etapa, verificador, cancellationToken)
            .ConfigureAwait(false);
        if (autorizada.IsFailure)
        {
            return GravacaoDaOperacao<RevisaoDasConcessoesDto>.Sem(Result<RevisaoDasConcessoesDto>.Failure(autorizada.Error!));
        }

        AutoriaDaVersao autoria = autorizada.Value!;
        return await _migracao
            .ExecutarAsync(perfilId, j => DecidirAsync(j, autoria, transicao, operacao, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DesfechoDaOperacao<RevisaoDasConcessoesDto>> DecidirAsync(
        RevisaoDaOperacao revisao,
        AutoriaDaVersao autoria,
        Func<RevisaoDaOperacao, AutoriaDaVersao, Result> transicao,
        OperacaoNaRevisaoDasConcessoes operacao,
        CancellationToken cancellationToken)
    {
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(revisao.Revisao, revisao.Contexto);
        Result resultado = transicao(revisao, autoria);
        if (resultado.IsFailure)
        {
            return new DesfechoDaOperacao<RevisaoDasConcessoesDto>(Result<RevisaoDasConcessoesDto>.ValidationFailure(resultado.Errors));
        }

        RevisaoDasConcessoesDto dto = RevisaoDasConcessoesMapping.ToDto(revisao.Revisao, revisao.Perfil, revisao.Contexto, autoria.Pessoa);
        IReadOnlyList<EnviarEmail> avisos = await AvisosAsync(revisao, antes, operacao, autoria.Pessoa, cancellationToken).ConfigureAwait(false);

        return new DesfechoDaOperacao<RevisaoDasConcessoesDto>(Result<RevisaoDasConcessoesDto>.Success(dto), Avisos: avisos);
    }
    private static ResultadoDaMigracao? LerDecisaoDaPessoa(string? codigo) =>
        CatalogoDe<ResultadoDaMigracao>.TentarObter(codigo, out ResultadoDaMigracao resultado)
            && resultado is ResultadoDaMigracao.Migrar or ResultadoDaMigracao.Recusar ? resultado : null;

}
