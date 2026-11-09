namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O cancelamento da versão pendente, com motivo. Cancela quem a criou, pela identidade, ou quem mantém perfis no
/// momento do cancelamento: a permissão se confere aqui, sob a trava do perfil, porque depende de quem criou a versão.
/// </summary>
public sealed class CancelamentoDaVersao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly EfeitosDoDesfechoDaVersao _efeitos;
    private readonly AutoriaDaOperacao _autoria;

    public CancelamentoDaVersao(OperacaoSobreOPerfil operacao, EfeitosDoDesfechoDaVersao efeitos, AutoriaDaOperacao autoria)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(autoria);

        _operacao = operacao;
        _efeitos = efeitos;
        _autoria = autoria;
    }

    public async Task<GravacaoDaOperacao<PerfilDto>> CancelarAsync(
        Guid perfilId, Guid versaoId, string? motivo, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        if (_autoria.Autor is not { } autor)
        {
            return GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(PerfilHandlerErros.AutorNaoIdentificado()));
        }

        GravacaoDaOperacao<PerfilDto> gravacao = await _operacao
            .ExecutarComAvisosAsync(
                perfilId,
                versaoId,
                (perfil, contexto) => CancelarNoPerfilAsync(perfil, versaoId, motivo, autor, contexto.Agora, verificador, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        return gravacao.Resultado.Error?.Code == PerfilErrorCodes.NaoEncontrado
            ? await RecusarSemPermissaoAsync(gravacao, perfilId, verificador, cancellationToken).ConfigureAwait(false)
            : gravacao;
    }

    // O perfil que não existe não tem autor de versão: quem não mantém perfis recebe a mesma recusa do perfil que existe.
    private static async Task<GravacaoDaOperacao<PerfilDto>> RecusarSemPermissaoAsync(
        GravacaoDaOperacao<PerfilDto> naoEncontrado, Guid perfilId, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        DecisaoDeAcesso decisao = await AcessoDaVersao.AvaliarManterAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);
        await AcessoDaVersao.RegistrarAsync(verificador, decisao, cancellationToken).ConfigureAwait(false);

        return decisao is DecisaoPermitida
            ? naoEncontrado
            : GravacaoDaOperacao<PerfilDto>.Sem(Result<PerfilDto>.Failure(PerfilErros.SemPermissaoNaEtapa()));
    }

    private async Task<DesfechoDaOperacao<PerfilDto>> CancelarNoPerfilAsync(
        Perfil perfil,
        Guid versaoId,
        string? motivo,
        Identidade autor,
        DateTimeOffset agora,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        if (!await PodeCancelarAsync(perfil, versaoId, autor, verificador, cancellationToken).ConfigureAwait(false))
        {
            return new DesfechoDaOperacao<PerfilDto>(Result<PerfilDto>.Failure(PerfilErros.SemPermissaoNaEtapa()));
        }

        DesfechoDaVersao desfecho = perfil.CancelarVersao(versaoId, autor, MotivosDaVersao.Normalizar(motivo), agora);

        return await _efeitos.AplicarAsync(perfil, desfecho, autor, cancellationToken).ConfigureAwait(false);
    }

    // Só quem criou a versão cancela sem a permissão; a versão que não existe também a pede, para não revelar o que há.
    private static async Task<bool> PodeCancelarAsync(
        Perfil perfil, Guid versaoId, Identidade autor, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (perfil.Versoes.FirstOrDefault(v => v.Id == versaoId) is { } versao && versao.CriadaPor.Equals(autor))
        {
            return true;
        }

        DecisaoDeAcesso decisao = await AcessoDaVersao.AvaliarManterAsync(verificador, perfil.Id, cancellationToken).ConfigureAwait(false);
        await AcessoDaVersao.RegistrarAsync(verificador, decisao, cancellationToken).ConfigureAwait(false);

        return decisao is DecisaoPermitida;
    }
}
