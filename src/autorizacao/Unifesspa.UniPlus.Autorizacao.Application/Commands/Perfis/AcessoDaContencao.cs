namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o solicitante pode fazer na contenção do perfil: a gestão de acessos, que mantém perfis, e a proteção de dados.
/// Cada capacidade é avaliada pelo ponto de decisão único, sem registrar: quem usa o resultado grava a decisão que
/// governou, para que a trilha traga a real.
/// </summary>
internal sealed record AcessoDaContencao(DecisaoDeAcesso DecisaoDaGestao, DecisaoDeAcesso DecisaoDaProtecaoDeDados)
{
    /// <summary>Mantém perfis: decide a etapa da gestão de acessos.</summary>
    public bool Gestao => DecisaoDaGestao is DecisaoPermitida;

    public bool ProtecaoDeDados => DecisaoDaProtecaoDeDados is DecisaoPermitida;

    /// <summary>Tem alguma das duas permissões.</summary>
    public bool Habilitado => Gestao || ProtecaoDeDados;

    /// <summary>
    /// O papel que a permissão deu à pessoa: com as duas, a gestão de acessos. Só vale para quem está
    /// <see cref="Habilitado"/>.
    /// </summary>
    public EtapaDaVersao Papel => Gestao ? EtapaDaVersao.GestaoDeAcessos : EtapaDaVersao.ProtecaoDeDados;

    public bool Decide(EtapaDaVersao etapa) => etapa == EtapaDaVersao.GestaoDeAcessos ? Gestao : ProtecaoDeDados;

    public DecisaoDeAcesso DecisaoDa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? DecisaoDaGestao : DecisaoDaProtecaoDeDados;

    /// <summary>Consulta as duas permissões, sem registrar. Nulo quando o token não identifica o solicitante por completo.</summary>
    public static async Task<AcessoDaContencao?> ConsultarAsync(IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso gestao = await AcessoDaVersao.AvaliarManterAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false);

        return gestao is SemSujeito
            ? null
            : new AcessoDaContencao(
                gestao, await AcessoDaVersao.AvaliarProtecaoDeDadosAsync(verificador, perfilId, cancellationToken).ConfigureAwait(false));
    }

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

    /// <summary>Registra o desfecho quando nenhuma etapa o governou: as permitidas que habilitaram ou, se nenhuma, as negativas.</summary>
    public Task RegistrarDoDesfechoAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync([DecisaoDaGestao, DecisaoDaProtecaoDeDados], IntencaoDoRegistro.Aprovador, cancellationToken);
    }
}
