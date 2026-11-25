namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão de uma etapa da aprovação da versão: aprovar ou recusar. A permissão da etapa é conferida aqui, antes de
/// qualquer leitura de concessão ou de perfil, e o desfecho que governou vai para a trilha de acesso. O resto roda na ordem de travas de
/// <see cref="OperacaoSobreOPerfil"/>, e a regra de decisão vive no agregado.
/// </summary>
public sealed class AprovacoesDaVersao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly EfeitosDoDesfechoDaVersao _efeitos;
    private readonly AutoriaDaOperacao _autoria;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly ConferenciaDoCatalogoNaVersao _conferencia;

    public AprovacoesDaVersao(
        OperacaoSobreOPerfil operacao,
        LeitorDoContextoDaVersao leitor,
        EfeitosDoDesfechoDaVersao efeitos,
        AutoriaDaOperacao autoria,
        ICatalogoDePermissoesConsultavel catalogo,
        ConferenciaDoCatalogoNaVersao conferencia)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(conferencia);

        _operacao = operacao;
        _leitor = leitor;
        _efeitos = efeitos;
        _autoria = autoria;
        _catalogo = catalogo;
        _conferencia = conferencia;
    }

    public async Task<GravacaoDaOperacao<PerfilDto>> DecidirAsync(
        DecisaoSolicitada solicitada, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitada);
        ArgumentNullException.ThrowIfNull(verificador);

        Result<AutoriaDaVersao> autorizada = await AutorizarAsync(solicitada, verificador, cancellationToken).ConfigureAwait(false);

        return autorizada.IsFailure
            ? GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(autorizada.Error!))
            : await _operacao
                .ExecutarComAvisosAsync(
                    solicitada.PerfilId,
                    (perfil, contexto) => DecidirNoPerfilAsync(perfil, Pedido(solicitada, autorizada.Value!, contexto.Agora), cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<Result<AutoriaDaVersao>> AutorizarAsync(
        DecisaoSolicitada solicitada, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoDasEtapas? acesso = await AcessoDaVersao.ConsultarAsync(verificador, solicitada.PerfilId, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = acesso is null
            ? PerfilHandlerErros.AutorNaoIdentificado()
            : await acesso.RecusaDaEtapaAsync((CatalogoDe<EtapaDaVersao>.TentarObter(solicitada.Etapa, out EtapaDaVersao valorEtapaDaVersao) ? valorEtapaDaVersao : (EtapaDaVersao?)null), verificador, cancellationToken)
                .ConfigureAwait(false);

        return recusa is null
            ? await _autoria.ExigirDaVersaoAsync(solicitada.PerfilId, cancellationToken).ConfigureAwait(false)
            : Result<AutoriaDaVersao>.Failure(recusa);
    }

    private static PedidoDeDecisaoDaVersao Pedido(DecisaoSolicitada solicitada, AutoriaDaVersao autoria, DateTimeOffset agora) =>
        new(
            solicitada.VersaoId,
            CatalogoDe<EtapaDaVersao>.Obter(solicitada.Etapa!),
            solicitada.Resultado,
            MotivoInformado.Normalizar(solicitada.Motivo),
            autoria,
            agora);

    private async Task<DesfechoDaOperacao<PerfilDto>> DecidirNoPerfilAsync(
        Perfil perfil, PedidoDeDecisaoDaVersao pedida, CancellationToken cancellationToken)
    {
        VersaoDoPerfil? versao = perfil.Versoes.FirstOrDefault(v => v.Id == pedida.VersaoId);
        ContextoDaVersao contexto = versao is null
            ? new ContextoDaVersao(false, false)
            : await _leitor.LerAsync(perfil.Id, versao.Nome, cancellationToken).ConfigureAwait(false);

        await _conferencia.ExigirCatalogoAtualAsync(cancellationToken).ConfigureAwait(false);
        DesfechoDaVersao desfecho = perfil.Decidir(pedida, contexto, _catalogo);

        return await _efeitos.AplicarAsync(perfil, desfecho, pedida.Autoria.Pessoa, cancellationToken).ConfigureAwait(false);
    }
}
