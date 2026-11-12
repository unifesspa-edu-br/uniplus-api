namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O que o solicitante da requisição pode fazer na revisão dos acessos: conduzi-la como Representante do controlador e
/// decidir os itens dela, rever como gestor de acessos os itens de quem é Representante, ou só consultar, como o
/// Encarregado de dados. Cada capacidade é avaliada pelo ponto de decisão único, por permissão do catálogo, sem registrar:
/// quem usa o resultado grava a decisão que governou o desfecho, para que a trilha traga a decisão real e não a de uma
/// permissão que a operação não exigia. A consulta só lê: nunca abre revisão nem decide item.
/// </summary>
internal sealed record AcessoDaRevisao(
    DecisaoDeAcesso DecisaoConduzir, DecisaoDeAcesso DecisaoReverProtecaoDeDados, DecisaoDeAcesso DecisaoConsultar)
{
    private static readonly PermissionRequirement PermissaoConduzir = Requisito(PermissaoAutorizacao.RevisoesConduzir);

    private static readonly PermissionRequirement PermissaoReverProtecaoDeDados = Requisito(PermissaoAutorizacao.RevisoesReverProtecaoDeDados);

    private static readonly PermissionRequirement PermissaoConsultar = Requisito(PermissaoAutorizacao.RevisoesConsultar);

    /// <summary>Conduz a revisão como Representante do controlador.</summary>
    public bool Conduz => DecisaoConduzir is DecisaoPermitida;

    /// <summary>Revê como gestor de acessos os itens de quem é Representante.</summary>
    public bool ReveProtecaoDeDados => DecisaoReverProtecaoDeDados is DecisaoPermitida;

    /// <summary>Consulta as revisões sem abrir nem decidir.</summary>
    public bool Consulta => DecisaoConsultar is DecisaoPermitida;

    /// <summary>Decide itens de alguma governança: quem conduz ou quem revê.</summary>
    public bool DecideAlgum => Conduz || ReveProtecaoDeDados;

    /// <summary>Lê as revisões: quem decide algum item ou quem consulta.</summary>
    public bool Le => DecideAlgum || Consulta;

    /// <summary>A decisão permitida que deu a leitura, da qual sai o registro de cada revisão exibida; nula sem leitura.</summary>
    public DecisaoPermitida? Leitura => DasDecisoesDeLeitura.OfType<DecisaoPermitida>().FirstOrDefault();

    private DecisaoDeAcesso[] DasDecisoesDeLeitura => [DecisaoConduzir, DecisaoReverProtecaoDeDados, DecisaoConsultar];

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
                conduzir,
                await AvaliarAsync(verificador, PermissaoReverProtecaoDeDados, revisaoId, cancellationToken).ConfigureAwait(false),
                await AvaliarAsync(verificador, PermissaoConsultar, revisaoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Registra o desfecho da leitura: as permissões que a habilitaram ou, se nenhuma habilitou, as negativas das três.
    /// </summary>
    public Task RegistrarLeituraAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        RegistrarAsync(verificador, DasDecisoesDeLeitura, cancellationToken);

    /// <summary>
    /// Registra o desfecho do pedido de decidir um item que ainda não sabe qual governança o governa: as permissões de
    /// decidir que o habilitaram ou, se nenhuma habilitou, as negativas delas. A consulta não governa decisão nenhuma e
    /// não entra.
    /// </summary>
    public Task RegistrarDaDecisaoAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        RegistrarAsync(verificador, [DecisaoConduzir, DecisaoReverProtecaoDeDados], cancellationToken);

    /// <summary>Registra o desfecho da decisão que governou a operação, permitida ou negada.</summary>
    public static Task RegistrarAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<DecisaoDeAcesso> decisoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync(decisoes, IntencaoDoRegistro.Aprovador, cancellationToken);
    }

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, Guid? revisaoId, CancellationToken cancellationToken)
    {
        ResourceContext? recurso = revisaoId is { } id
            ? ResourceContext.From(ResourceTiposAutorizacao.Revisao, permissao.Sensibilidade, resourceId: id).Value
            : null;

        return verificador.AvaliarAsync(permissao, recurso, cancellationToken);
    }

    private static PermissionRequirement Requisito(PermissaoAutorizacao permissao) =>
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;
}
