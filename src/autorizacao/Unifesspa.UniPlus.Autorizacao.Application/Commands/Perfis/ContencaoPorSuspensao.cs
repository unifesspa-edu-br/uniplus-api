namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A suspensão do perfil e a retomada dela, por etapa. A permissão se confere aqui, antes de qualquer leitura de perfil,
/// e o desfecho que governou vai para a trilha de acesso; o resto roda na ordem de travas de
/// <see cref="OperacaoSobreOPerfil"/>, com o instante e a concessão do decisor lidos sob a trava, e a regra vive no
/// agregado.
/// </summary>
public sealed class ContencaoPorSuspensao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly AutoriaDaOperacao _autoria;
    private readonly AvisosDaContencao _avisos;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public ContencaoPorSuspensao(
        OperacaoSobreOPerfil operacao, AutoriaDaOperacao autoria, AvisosDaContencao avisos, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(catalogo);

        _operacao = operacao;
        _autoria = autoria;
        _avisos = avisos;
        _catalogo = catalogo;
    }

    /// <summary>Suspende o perfil: a gestão de acessos ou a proteção de dados, com motivo; o aviso sai na mesma gravação.</summary>
    public async Task<GravacaoDaOperacao<bool>> SuspenderAsync(
        Guid perfilId, string? motivo, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        Result<PedidoDeSuspensao> pedido = await PedidoAutorizadoAsync(perfilId, motivo, verificador, cancellationToken).ConfigureAwait(false);
        if (pedido.IsFailure)
        {
            return GravacaoDaOperacao<bool>.Sem(Result<bool>.Failure(pedido.Error!));
        }

        return await _operacao
            .ExecutarComAvisosAsync(
                perfilId,
                (perfil, contexto) => SuspenderNoPerfilAsync(perfil, pedido.Value!, contexto.Agora, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Decide uma etapa da retomada; a suspensão se fecha quando as etapas exigidas agora estão decididas.</summary>
    public async Task<Result<RetomadaDaSuspensaoDto>> RetomarAsync(
        Guid perfilId, string? etapa, string? motivo, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        EtapaDaVersao? pedida = EtapaDaVersaoCodigo.TentarLer(etapa);
        DomainError? recusa = await RecusaDaRetomadaAsync(perfilId, pedida, verificador, cancellationToken).ConfigureAwait(false);

        return recusa is not null
            ? Result<RetomadaDaSuspensaoDto>.Failure(recusa)
            : await _operacao
                .ExecutarAsync(
                    perfilId,
                    (perfil, contexto) => RetomarNoPerfilAsync(perfil, pedida!.Value, motivo, contexto.Agora, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>Quem suspende, com o papel que a permissão lhe deu, e por quê; a recusa de acesso responde antes da identidade do token.</summary>
    private async Task<Result<PedidoDeSuspensao>> PedidoAutorizadoAsync(
        Guid perfilId, string? motivo, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoDasEtapas? acesso = await AcessoDaVersao.ConsultarParaContencaoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        DomainError? recusa = await RecusaDeSuspenderAsync(acesso, verificador, cancellationToken).ConfigureAwait(false);

        return recusa is null && _autoria.Autor is { } autor
            ? Result<PedidoDeSuspensao>.Success(new PedidoDeSuspensao(autor, acesso!.Papel, motivo))
            : Result<PedidoDeSuspensao>.Failure(recusa ?? PerfilHandlerErros.AutorNaoIdentificado());
    }

    private static async Task<DomainError?> RecusaDaRetomadaAsync(
        Guid perfilId, EtapaDaVersao? pedida, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoDasEtapas? acesso = await AcessoDaVersao.ConsultarParaContencaoAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return acesso is null
            ? PerfilHandlerErros.AutorNaoIdentificado()
            : await acesso.RecusaDaEtapaAsync(pedida, verificador, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainError?> RecusaDeSuspenderAsync(
        AcessoDasEtapas? acesso, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (acesso is null)
        {
            return PerfilHandlerErros.AutorNaoIdentificado();
        }

        if (!acesso.Habilitado)
        {
            await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
            return PerfilErros.SemPermissaoNaEtapa();
        }

        await AcessoDaVersao.RegistrarAsync(verificador, acesso.DecisaoDa(acesso.Papel), cancellationToken).ConfigureAwait(false);

        return null;
    }

    private async Task<DesfechoDaOperacao<bool>> SuspenderNoPerfilAsync(
        Perfil perfil, PedidoDeSuspensao pedido, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result suspensao = perfil.Suspender(pedido.Autor, pedido.Papel, pedido.Motivo, agora, _catalogo);
        if (suspensao.IsFailure)
        {
            return new DesfechoDaOperacao<bool>(Result<bool>.ValidationFailure(suspensao.Errors));
        }

        return new DesfechoDaOperacao<bool>(
            Result<bool>.Success(true),
            Avisos: await _avisos.DaSuspensaoAsync(perfil, perfil.SuspensaoAberta!, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<RetomadaDaSuspensaoDto>> RetomarNoPerfilAsync(
        Perfil perfil, EtapaDaVersao etapa, string? motivo, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result<AutoriaDaVersao> autoria = await _autoria.ExigirDaVersaoAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        if (autoria.IsFailure)
        {
            return Result<RetomadaDaSuspensaoDto>.Failure(autoria.Error!);
        }

        Result decisao = perfil.DecidirRetomada(new ContextoDaRetomada(autoria.Value!.Pessoa, autoria.Value.TemConcessao, etapa, MotivoInformado.Normalizar(motivo), _catalogo, agora));

        return decisao.IsFailure
            ? Result<RetomadaDaSuspensaoDto>.ValidationFailure(decisao.Errors)
            : Result<RetomadaDaSuspensaoDto>.Success(new RetomadaDaSuspensaoDto(
                perfil.SuspensaoAberta is null,
                [.. perfil.EtapasPendentesDaRetomada(_catalogo).Select(static e => e.ToCodigo())]));
    }

    private sealed record PedidoDeSuspensao(Identidade Autor, EtapaDaVersao Papel, string? Motivo);
}
