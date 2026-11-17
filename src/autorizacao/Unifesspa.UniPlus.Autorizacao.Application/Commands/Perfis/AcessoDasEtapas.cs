namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As permissões das duas etapas da governança da versão, a da gestão de acessos e a da proteção de dados, avaliadas sem
/// registrar: quem usa o resultado grava a decisão que governou o desfecho, para que a trilha traga a real e não uma
/// negativa de uma permissão que a pessoa nem precisava ter. A permissão que habilita a gestão varia com a operação
/// (aprovar pela gestão ou manter perfis), e <see cref="AcessoDaVersao"/> escolhe qual.
/// </summary>
internal sealed record AcessoDasEtapas(DecisaoDeAcesso DecisaoDaGestao, DecisaoDeAcesso DecisaoDaProtecaoDeDados)
{
    /// <summary>Decide a etapa da gestão de acessos.</summary>
    public bool Gestao => DecisaoDaGestao is DecisaoPermitida;

    /// <summary>Decide a etapa da proteção de dados.</summary>
    public bool ProtecaoDeDados => DecisaoDaProtecaoDeDados is DecisaoPermitida;

    /// <summary>Tem a permissão de alguma das duas etapas.</summary>
    public bool Habilitado => Gestao || ProtecaoDeDados;

    /// <summary>
    /// O papel que a permissão deu à pessoa: com as duas, a gestão de acessos. Só vale para quem está
    /// <see cref="Habilitado"/>.
    /// </summary>
    public EtapaDaVersao Papel => Gestao ? EtapaDaVersao.GestaoDeAcessos : EtapaDaVersao.ProtecaoDeDados;

    public bool Decide(EtapaDaVersao etapa) => etapa == EtapaDaVersao.GestaoDeAcessos ? Gestao : ProtecaoDeDados;

    public DecisaoDeAcesso DecisaoDa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? DecisaoDaGestao : DecisaoDaProtecaoDeDados;

    /// <summary>
    /// A recusa de quem não pode decidir a etapa pedida, registrando a decisão que governou: sem nenhuma das permissões é
    /// 403; com a da outra etapa, ou com etapa inexistente, a etapa pedida não é a da pessoa. Nulo quando pode.
    /// </summary>
    public async Task<DomainError?> RecusaDaEtapaAsync(EtapaDaVersao? etapa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (!Habilitado || etapa is null)
        {
            await RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
            return Habilitado ? PerfilErros.EtapaInvalida() : PerfilErros.SemPermissaoNaEtapa();
        }

        await AcessoDaVersao.RegistrarAsync(verificador, DecisaoDa(etapa.Value), cancellationToken).ConfigureAwait(false);

        return Decide(etapa.Value) ? null : PerfilErros.EtapaInvalida();
    }

    /// <summary>
    /// Registra o desfecho quando nenhuma etapa o governou: as decisões permitidas que habilitaram ou, se nenhuma
    /// habilitou, as negativas, que são a tentativa real.
    /// </summary>
    public Task RegistrarDoDesfechoAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync([DecisaoDaGestao, DecisaoDaProtecaoDeDados], IntencaoDoRegistro.Aprovador, cancellationToken);
    }
}
