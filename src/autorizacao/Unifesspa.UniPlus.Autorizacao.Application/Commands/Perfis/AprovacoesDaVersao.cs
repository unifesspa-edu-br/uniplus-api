namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão de uma etapa da aprovação da versão: aprovar ou recusar. A permissão da etapa é conferida aqui, antes de
/// qualquer leitura do perfil, e o desfecho que governou vai para a trilha de acesso. O resto roda na ordem de travas de
/// <see cref="OperacaoSobreOPerfil"/>, e a regra de decisão vive no agregado.
/// </summary>
public sealed class AprovacoesDaVersao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly EfeitosDoDesfechoDaVersao _efeitos;
    private readonly AutoriaDaOperacao _autoria;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public AprovacoesDaVersao(
        OperacaoSobreOPerfil operacao,
        LeitorDoContextoDaVersao leitor,
        EfeitosDoDesfechoDaVersao efeitos,
        AutoriaDaOperacao autoria,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        _operacao = operacao;
        _leitor = leitor;
        _efeitos = efeitos;
        _autoria = autoria;
        _catalogo = catalogo;
    }

    public async Task<GravacaoDaOperacao<PerfilDto>> DecidirAsync(
        DecisaoSolicitada solicitada, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitada);
        ArgumentNullException.ThrowIfNull(verificador);

        Result<PedidoDeDecisaoDaVersao> pedida = await PedirAsync(solicitada, verificador, cancellationToken).ConfigureAwait(false);
        if (pedida.IsFailure)
        {
            return GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(pedida.Error!));
        }

        return await _operacao
            .ExecutarComAvisosAsync(solicitada.PerfilId, perfil => DecidirNoPerfilAsync(perfil, pedida.Value!, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<PedidoDeDecisaoDaVersao>> PedirAsync(
        DecisaoSolicitada solicitada, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoDaVersao? acesso = await AcessoDaVersao.ConsultarAsync(verificador, solicitada.PerfilId, cancellationToken).ConfigureAwait(false);
        AutoriaDaVersao? autoria = await _autoria.DaVersaoAsync(solicitada.PerfilId, cancellationToken).ConfigureAwait(false);
        if (acesso is null || autoria is null)
        {
            return Result<PedidoDeDecisaoDaVersao>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
        }

        DomainError? recusa = await RecusaDoAcessoAsync(acesso, EtapaDaVersaoCodigo.TentarLer(solicitada.Etapa), verificador, cancellationToken)
            .ConfigureAwait(false);

        return recusa is not null
            ? Result<PedidoDeDecisaoDaVersao>.Failure(recusa)
            : Result<PedidoDeDecisaoDaVersao>.Success(Pedido(solicitada, autoria));
    }

    private PedidoDeDecisaoDaVersao Pedido(DecisaoSolicitada solicitada, AutoriaDaVersao autoria) =>
        new(
            solicitada.VersaoId,
            EtapaDaVersaoCodigo.FromCodigo(solicitada.Etapa!),
            solicitada.Resultado,
            MotivosDaVersao.Normalizar(solicitada.Motivo),
            autoria,
            _autoria.Agora);

    // Sem nenhuma das permissões é 403; com só a da outra etapa, a etapa pedida não é a da pessoa. A trilha recebe a
    // decisão que governou: a da etapa pedida quando ela existe, senão o desfecho das duas.
    private static async Task<DomainError?> RecusaDoAcessoAsync(
        AcessoDaVersao acesso, EtapaDaVersao? etapa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (!acesso.Aprova)
        {
            await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
            return PerfilErros.SemPermissaoNaEtapa();
        }

        if (etapa is not { } pedida)
        {
            await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
            return PerfilErros.EtapaInvalida();
        }

        await AcessoDaVersao.RegistrarAsync(verificador, acesso.DecisaoDa(pedida), cancellationToken).ConfigureAwait(false);

        return acesso.Decide(pedida) ? null : PerfilErros.EtapaInvalida();
    }

    private async Task<DesfechoDaOperacao<PerfilDto>> DecidirNoPerfilAsync(
        Perfil perfil, PedidoDeDecisaoDaVersao pedida, CancellationToken cancellationToken)
    {
        VersaoDoPerfil? versao = perfil.Versoes.FirstOrDefault(v => v.Id == pedida.VersaoId);
        ContextoDaVersao contexto = versao is null
            ? new ContextoDaVersao(false, false)
            : await _leitor.LerAsync(perfil.Id, versao.Nome, cancellationToken).ConfigureAwait(false);

        DesfechoDaVersao desfecho = perfil.Decidir(pedida, contexto, _catalogo);

        return await _efeitos.AplicarAsync(perfil, desfecho, pedida.Autoria.Pessoa, cancellationToken).ConfigureAwait(false);
    }
}
