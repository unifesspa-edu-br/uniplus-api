namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que o solicitante da requisição pode fazer na revisão dos acessos: conduzi-la como Representante do controlador e
/// decidir os itens dela, ou rever como gestor de acessos os itens de quem é Representante. Cada capacidade é avaliada pelo ponto de
/// decisão único, por permissão do catálogo, sem registrar: quem usa o resultado grava a decisão que governou o desfecho
/// (<see cref="RegistrarAsync"/>), para que a trilha traga a decisão real e não a de uma permissão que a operação não
/// exigia.
/// </summary>
internal sealed record AcessoDaRevisao(DecisaoDeAcesso DecisaoConduzir, DecisaoDeAcesso DecisaoReverProtecaoDeDados)
{
    private static readonly PermissionRequirement PermissaoConduzir =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.RevisoesConduzir).Requisito;

    private static readonly PermissionRequirement PermissaoReverProtecaoDeDados =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.RevisoesReverProtecaoDeDados).Requisito;

    /// <summary>Conduz a revisão como Representante do controlador.</summary>
    public bool Conduz => DecisaoConduzir is DecisaoPermitida;

    /// <summary>Revê como gestor de acessos os itens de quem é Representante.</summary>
    public bool ReveProtecaoDeDados => DecisaoReverProtecaoDeDados is DecisaoPermitida;

    /// <summary>Lê as revisões: quem conduz ou quem revê.</summary>
    public bool Le => Conduz || ReveProtecaoDeDados;

    /// <summary>A permissão de quem decide o item: o Representante decide os comuns, e os gestores, os de quem é Representante.</summary>
    public bool Decide(TipoDeGovernanca quemDecide) =>
        quemDecide == TipoDeGovernanca.RepresentanteDoControlador ? Conduz : ReveProtecaoDeDados;

    /// <summary>A decisão da permissão de quem decide os itens da governança.</summary>
    public DecisaoDeAcesso DecisaoDe(TipoDeGovernanca quemDecide) =>
        quemDecide == TipoDeGovernanca.RepresentanteDoControlador ? DecisaoConduzir : DecisaoReverProtecaoDeDados;

    /// <summary>
    /// Consulta o que a requisição pode, sem registrar. Nulo quando o token não identifica o solicitante por completo: a
    /// borda responde 401, e nenhuma interpretação de acesso se faz antes disso.
    /// </summary>
    /// <param name="revisaoId">A revisão acessada, quando a consulta é sobre uma; a trilha então nomeia a revisão.</param>
    public static async Task<AcessoDaRevisao?> ConsultarAsync(
        IVerificadorDeAcesso verificador, Guid? revisaoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso conduzir = await AvaliarAsync(verificador, PermissaoConduzir, revisaoId, cancellationToken).ConfigureAwait(false);

        return conduzir is SemSujeito
            ? null
            : new AcessoDaRevisao(
                conduzir, await AvaliarAsync(verificador, PermissaoReverProtecaoDeDados, revisaoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Registra o desfecho das duas permissões: as que habilitaram ou, se nenhuma habilitou, as negativas. É o caso da
    /// leitura e do pedido que ainda não sabe qual governança o governa.
    /// </summary>
    public Task RegistrarAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        RegistrarAsync(verificador, [DecisaoConduzir, DecisaoReverProtecaoDeDados], cancellationToken);

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, Guid? revisaoId, CancellationToken cancellationToken)
    {
        ResourceContext? recurso = revisaoId is { } id
            ? ResourceContext.From(ResourceTiposAutorizacao.Revisao, permissao.Sensibilidade, resourceId: id).Value
            : null;

        return verificador.AvaliarAsync(permissao, recurso, cancellationToken);
    }

    /// <summary>Registra o desfecho da decisão que governou a operação, permitida ou negada.</summary>
    public static Task RegistrarAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<DecisaoDeAcesso> decisoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync(decisoes, IntencaoDoRegistro.Aprovador, cancellationToken);
    }
}
