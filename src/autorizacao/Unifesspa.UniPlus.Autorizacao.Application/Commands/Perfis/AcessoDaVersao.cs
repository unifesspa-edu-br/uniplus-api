namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que o solicitante pode fazer na governança das versões de um perfil: decidir cada etapa da aprovação, manter perfis
/// e ler o documento. Cada capacidade é avaliada pelo ponto de decisão único, sem registrar: quem usa o resultado grava
/// a decisão que governou o desfecho, para que a trilha traga a real e não uma negativa de uma permissão que a pessoa nem
/// precisava ter.
/// </summary>
internal sealed record AcessoDaVersao(DecisaoDeAcesso DecisaoDaGestao, DecisaoDeAcesso DecisaoDaProtecaoDeDados)
{
    private static readonly PermissionRequirement PermissaoDaGestao = Requisito(PermissaoAutorizacao.PerfisAprovarFuncional);

    private static readonly PermissionRequirement PermissaoDaProtecaoDeDados =
        Requisito(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados);

    private static readonly PermissionRequirement PermissaoDeManter = Requisito(PermissaoAutorizacao.PerfisManter);

    /// <summary>Decide a etapa da gestão de acessos.</summary>
    public bool Gestao => DecisaoDaGestao is DecisaoPermitida;

    /// <summary>Decide a etapa da proteção de dados.</summary>
    public bool ProtecaoDeDados => DecisaoDaProtecaoDeDados is DecisaoPermitida;

    /// <summary>Decide alguma das duas etapas.</summary>
    public bool Aprova => Gestao || ProtecaoDeDados;

    public bool Decide(EtapaDaVersao etapa) => etapa == EtapaDaVersao.GestaoDeAcessos ? Gestao : ProtecaoDeDados;

    public DecisaoDeAcesso DecisaoDa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? DecisaoDaGestao : DecisaoDaProtecaoDeDados;

    /// <summary>Consulta as duas etapas, sem registrar. Nulo quando o token não identifica o solicitante por completo.</summary>
    public static async Task<AcessoDaVersao?> ConsultarAsync(IVerificadorDeAcesso verificador, Guid? perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso gestao = await AvaliarAsync(verificador, PermissaoDaGestao, perfilId, cancellationToken).ConfigureAwait(false);
        if (gestao is SemSujeito)
        {
            return null;
        }

        return new AcessoDaVersao(
            gestao, await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, perfilId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Se a pessoa mantém perfis; a avaliação não registra nada.</summary>
    public static async Task<DecisaoDeAcesso> AvaliarManterAsync(
        IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return await AvaliarAsync(verificador, PermissaoDeManter, perfilId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Se a pessoa aprova pela proteção de dados; a avaliação não registra nada.</summary>
    public static async Task<DecisaoDeAcesso> AvaliarProtecaoDeDadosAsync(
        IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, perfilId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Se a pessoa lê o documento da versão: quem mantém perfis ou quem aprova pela proteção de dados. Registra o desfecho
    /// de uma só vez, as permissões que habilitaram ou, se nenhuma habilitou, as negativas.
    /// </summary>
    public static async Task<bool> PodeLerODocumentoAsync(
        IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso manter = await AvaliarAsync(verificador, PermissaoDeManter, perfilId, cancellationToken).ConfigureAwait(false);
        DecisaoDeAcesso protecao = await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, perfilId, cancellationToken).ConfigureAwait(false);
        await verificador.RegistrarDesfechoAsync([manter, protecao], IntencaoDoRegistro.Aprovador, cancellationToken).ConfigureAwait(false);

        return manter is DecisaoPermitida || protecao is DecisaoPermitida;
    }

    /// <summary>
    /// Se a pessoa lê a janela de migração: quem mantém perfis ou aprova por qualquer das etapas. Registra o desfecho de
    /// uma só vez, as permissões que habilitaram ou, se nenhuma habilitou, as negativas.
    /// </summary>
    public static async Task<bool> PodeLerAJanelaAsync(IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso manter = await AvaliarAsync(verificador, PermissaoDeManter, perfilId, cancellationToken).ConfigureAwait(false);
        DecisaoDeAcesso gestao = await AvaliarAsync(verificador, PermissaoDaGestao, perfilId, cancellationToken).ConfigureAwait(false);
        DecisaoDeAcesso protecao = await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, perfilId, cancellationToken).ConfigureAwait(false);
        await verificador.RegistrarDesfechoAsync([manter, gestao, protecao], IntencaoDoRegistro.Aprovador, cancellationToken).ConfigureAwait(false);

        return manter is DecisaoPermitida || gestao is DecisaoPermitida || protecao is DecisaoPermitida;
    }

    /// <summary>
    /// Se a pessoa lista a fila da etapa: a permissão da etapa governa, e a decisão dela vai para a trilha. Nulo quando
    /// o token não identifica a pessoa.
    /// </summary>
    public static async Task<bool?> PodeListarAsync(IVerificadorDeAcesso verificador, EtapaDaVersao fila, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso decisao = await AvaliarAsync(
            verificador, fila == EtapaDaVersao.GestaoDeAcessos ? PermissaoDaGestao : PermissaoDaProtecaoDeDados, null, cancellationToken)
            .ConfigureAwait(false);
        if (decisao is SemSujeito)
        {
            return null;
        }

        await RegistrarAsync(verificador, decisao, cancellationToken).ConfigureAwait(false);

        return decisao is DecisaoPermitida;
    }

    /// <summary>
    /// Se a pessoa vê as divergências da governança: quem mantém perfis ou aprova pela proteção de dados. Registra o
    /// desfecho de uma só vez. Nulo quando o token não identifica a pessoa.
    /// </summary>
    public static async Task<bool?> PodeVerAGovernancaAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso manter = await AvaliarAsync(verificador, PermissaoDeManter, null, cancellationToken).ConfigureAwait(false);
        if (manter is SemSujeito)
        {
            return null;
        }

        DecisaoDeAcesso protecao = await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, null, cancellationToken).ConfigureAwait(false);
        await verificador.RegistrarDesfechoAsync([manter, protecao], IntencaoDoRegistro.Aprovador, cancellationToken).ConfigureAwait(false);

        return manter is DecisaoPermitida || protecao is DecisaoPermitida;
    }

    /// <summary>Registra a decisão que governou o desfecho, permitida ou negada.</summary>
    public static Task RegistrarAsync(IVerificadorDeAcesso verificador, DecisaoDeAcesso decisao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync([decisao], IntencaoDoRegistro.Aprovador, cancellationToken);
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

    private static PermissionRequirement Requisito(PermissaoAutorizacao permissao) =>
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, Guid? perfilId, CancellationToken cancellationToken) =>
        verificador.AvaliarAsync(
            permissao,
            perfilId is { } id ? ResourceContext.From(ResourceTiposAutorizacao.Perfil, permissao.Sensibilidade, resourceId: id).Value : null,
            cancellationToken);
}
