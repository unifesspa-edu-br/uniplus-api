namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que o solicitante pode fazer na governança das versões de um perfil: decidir cada etapa da aprovação, manter perfis
/// e ler o perfil, o documento, a janela, a contenção, a reclassificação e as divergências, o que quem só consulta as
/// versões, como o Encarregado de dados, também faz, sem decidir. Cada capacidade é avaliada pelo ponto de decisão único, sem registrar: quem usa o resultado grava
/// a decisão que governou o desfecho, para que a trilha traga a real e não uma negativa de uma permissão que a pessoa nem
/// precisava ter.
/// </summary>
internal sealed record AcessoDaVersao(DecisaoDeAcesso DecisaoDaGestao, DecisaoDeAcesso DecisaoDaProtecaoDeDados)
{
    private static readonly PermissionRequirement PermissaoDaGestao = Requisito(PermissaoAutorizacao.PerfisAprovarFuncional);

    private static readonly PermissionRequirement PermissaoDaProtecaoDeDados =
        Requisito(PermissaoAutorizacao.PerfisAprovarProtecaoDeDados);

    private static readonly PermissionRequirement PermissaoDeManter = Requisito(PermissaoAutorizacao.PerfisManter);

    private static readonly PermissionRequirement PermissaoDeConsultar = Requisito(PermissaoAutorizacao.PerfisConsultarVersoes);

    /// <summary>Decide a etapa da gestão de acessos.</summary>
    public bool Gestao => DecisaoDaGestao is DecisaoPermitida;

    /// <summary>Decide a etapa da proteção de dados.</summary>
    public bool ProtecaoDeDados => DecisaoDaProtecaoDeDados is DecisaoPermitida;

    /// <summary>Decide alguma das duas etapas.</summary>
    public bool Aprova => Gestao || ProtecaoDeDados;

    public bool Decide(EtapaDaVersao etapa) => etapa == EtapaDaVersao.GestaoDeAcessos ? Gestao : ProtecaoDeDados;

    public DecisaoDeAcesso DecisaoDa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? DecisaoDaGestao : DecisaoDaProtecaoDeDados;

    /// <summary>
    /// Consulta as duas etapas, sem registrar. O recurso da trilha é o perfil, ou o do tipo dado quando a operação é de
    /// outro, como a reclassificação. Nulo quando o token não identifica o solicitante por completo.
    /// </summary>
    public static async Task<AcessoDaVersao?> ConsultarAsync(
        IVerificadorDeAcesso verificador, Guid? recursoId, CancellationToken cancellationToken, string tipoDoRecurso = ResourceTiposAutorizacao.Perfil)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso gestao = await AvaliarAsync(verificador, PermissaoDaGestao, recursoId, cancellationToken, tipoDoRecurso).ConfigureAwait(false);
        if (gestao is SemSujeito)
        {
            return null;
        }

        return new AcessoDaVersao(
            gestao, await AvaliarAsync(verificador, PermissaoDaProtecaoDeDados, recursoId, cancellationToken, tipoDoRecurso).ConfigureAwait(false));
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
    /// Se a pessoa lê o documento da versão: quem mantém perfis, quem aprova pela proteção de dados ou quem consulta as
    /// versões. Registra o desfecho de uma só vez, as permissões que habilitaram ou, se nenhuma habilitou, as negativas.
    /// </summary>
    public static Task<AcessoAvaliado> AvaliarLeituraDoDocumentoAsync(
        IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken) =>
        AvaliarLeituraAsync(verificador, perfilId, ResourceTiposAutorizacao.Perfil, [PermissaoDeManter, PermissaoDaProtecaoDeDados, PermissaoDeConsultar], cancellationToken);

    /// <summary>
    /// Se a pessoa lê a trilha da contenção do perfil: quem mantém perfis, quem aprova pela proteção de dados ou quem
    /// consulta as versões. Registra o desfecho de uma só vez; a suspensão e a retomada continuam só de quem decide.
    /// </summary>
    public static Task<AcessoAvaliado> AvaliarLeituraDaContencaoAsync(
        IVerificadorDeAcesso verificador, Guid perfilId, CancellationToken cancellationToken) =>
        AvaliarLeituraAsync(verificador, perfilId, ResourceTiposAutorizacao.Perfil, [PermissaoDeManter, PermissaoDaProtecaoDeDados, PermissaoDeConsultar], cancellationToken);

    /// <summary>
    /// Se a pessoa lê o perfil ou a janela de migração dele: quem mantém perfis, aprova por qualquer das etapas ou consulta
    /// as versões. Sem <paramref name="perfilId"/>, a leitura é da coleção. Registra o desfecho de uma só vez, as
    /// permissões que habilitaram ou, se nenhuma habilitou, as negativas.
    /// </summary>
    public static Task<AcessoAvaliado> AvaliarLeituraDoPerfilAsync(IVerificadorDeAcesso verificador, Guid? perfilId, CancellationToken cancellationToken) =>
        AvaliarLeituraAsync(
            verificador, perfilId, ResourceTiposAutorizacao.Perfil, [PermissaoDeManter, PermissaoDaGestao, PermissaoDaProtecaoDeDados, PermissaoDeConsultar], cancellationToken);

    /// <summary>
    /// Se a pessoa lê o detalhe da reclassificação: quem decide alguma das duas etapas ou quem consulta as versões. A
    /// trilha registra a reclassificação recebida como recurso.
    /// </summary>
    public static Task<AcessoAvaliado> AvaliarLeituraDaReclassificacaoAsync(
        IVerificadorDeAcesso verificador, Guid reclassificacaoId, CancellationToken cancellationToken) =>
        AvaliarLeituraAsync(
            verificador, reclassificacaoId, ResourceTiposAutorizacao.Reclassificacao, [PermissaoDaGestao, PermissaoDaProtecaoDeDados, PermissaoDeConsultar], cancellationToken);

    /// <summary>
    /// Se a pessoa lista a fila da etapa: a permissão da etapa governa, e a decisão dela vai para a trilha.
    /// </summary>
    public static async Task<AcessoAvaliado> AvaliarListagemAsync(IVerificadorDeAcesso verificador, EtapaDaVersao fila, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso decisao = await AvaliarAsync(verificador, PermissaoDaEtapa(fila), null, cancellationToken).ConfigureAwait(false);
        if (decisao is SemSujeito)
        {
            return AcessoAvaliado.SemAutorIdentificado;
        }

        return await RegistrarODesfechoAsync(verificador, [decisao], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Se a pessoa vê as divergências da governança: quem mantém perfis, aprova pela proteção de dados ou consulta as
    /// versões. Registra o desfecho de uma só vez.
    /// </summary>
    public static Task<AcessoAvaliado> AvaliarVisaoDaGovernancaAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        AvaliarLeituraAsync(
            verificador, null, ResourceTiposAutorizacao.Perfil, [PermissaoDeManter, PermissaoDaProtecaoDeDados, PermissaoDeConsultar], cancellationToken);

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

    /// <summary>
    /// Avalia as permissões que dão a leitura, na ordem dada, e registra o desfecho de uma só vez; o acesso é permitido
    /// quando alguma delas habilitou. O token que não identifica a pessoa responde antes de qualquer registro.
    /// </summary>
    private static async Task<AcessoAvaliado> AvaliarLeituraAsync(
        IVerificadorDeAcesso verificador,
        Guid? recursoId,
        string tipoDoRecurso,
        PermissionRequirement[] permissoes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        List<DecisaoDeAcesso> decisoes = [];
        foreach (PermissionRequirement permissao in permissoes)
        {
            decisoes.Add(await AvaliarAsync(verificador, permissao, recursoId, cancellationToken, tipoDoRecurso).ConfigureAwait(false));
        }

        return decisoes.Exists(static d => d is SemSujeito)
            ? AcessoAvaliado.SemAutorIdentificado
            : await RegistrarODesfechoAsync(verificador, decisoes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Registra o desfecho das decisões de uma só vez; o acesso é permitido quando alguma delas habilitou.</summary>
    private static async Task<AcessoAvaliado> RegistrarODesfechoAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<DecisaoDeAcesso> decisoes, CancellationToken cancellationToken)
    {
        await verificador.RegistrarDesfechoAsync(decisoes, IntencaoDoRegistro.Aprovador, cancellationToken).ConfigureAwait(false);

        return decisoes.Any(static d => d is DecisaoPermitida) ? AcessoAvaliado.Permitido : AcessoAvaliado.Negado;
    }

    private static PermissionRequirement PermissaoDaEtapa(EtapaDaVersao etapa) =>
        etapa == EtapaDaVersao.GestaoDeAcessos ? PermissaoDaGestao : PermissaoDaProtecaoDeDados;

    private static PermissionRequirement Requisito(PermissaoAutorizacao permissao) =>
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador,
        PermissionRequirement permissao,
        Guid? recursoId,
        CancellationToken cancellationToken,
        string tipoDoRecurso = ResourceTiposAutorizacao.Perfil) =>
        verificador.AvaliarAsync(
            permissao,
            recursoId is { } id ? ResourceContext.From(tipoDoRecurso, permissao.Sensibilidade, resourceId: id).Value : null,
            cancellationToken);
}
