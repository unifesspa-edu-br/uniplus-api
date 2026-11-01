namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;

/// <summary>
/// O que o solicitante da requisição pode fazer no fluxo dos pedidos: decidir cada etapa e administrar concessões.
/// Cada capacidade é avaliada pelo ponto de decisão único, por permissão do catálogo, sem registrar: quem usa o
/// resultado grava o que governou o desfecho (<see cref="RegistrarAsync"/> e <see cref="RegistrarDoDesfechoAsync"/>), para
/// que a trilha traga a decisão real e não uma negativa falsa de uma permissão que a pessoa nem precisava ter.
/// </summary>
internal sealed record AcessoDoAprovador(
    DecisaoDeAcesso DecisaoFuncional,
    DecisaoDeAcesso DecisaoProtecaoDeDados,
    DecisaoDeAcesso? DecisaoAdministrar)
{
    private static readonly PermissionRequirement PermissaoFuncional =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PedidosAprovarFuncional).Requisito;

    private static readonly PermissionRequirement PermissaoProtecaoDeDados =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PedidosAprovarProtecaoDeDados).Requisito;

    private static readonly PermissionRequirement PermissaoAdministrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    /// <summary>Decide a etapa do gestor de acessos.</summary>
    public bool Funcional => Permitida(DecisaoFuncional);

    /// <summary>Decide a etapa da proteção de dados.</summary>
    public bool ProtecaoDeDados => Permitida(DecisaoProtecaoDeDados);

    /// <summary>Administra concessões.</summary>
    public bool Administrar => Permitida(DecisaoAdministrar);

    /// <summary>Decide alguma das duas etapas.</summary>
    public bool Aprova => Funcional || ProtecaoDeDados;

    /// <summary>Vê o pedido de qualquer pessoa: decide uma etapa ou administra concessões.</summary>
    public bool VeTodos => Aprova || Administrar;

    /// <summary>A permissão da etapa que o pedido aguarda.</summary>
    public bool Decide(Domain.Enums.EtapaDoPedido etapa) =>
        etapa == Domain.Enums.EtapaDoPedido.Gestor ? Funcional : ProtecaoDeDados;

    /// <summary>A decisão da permissão da etapa que o pedido aguarda.</summary>
    public DecisaoDeAcesso DecisaoDaEtapa(Domain.Enums.EtapaDoPedido etapa) =>
        etapa == Domain.Enums.EtapaDoPedido.Gestor ? DecisaoFuncional : DecisaoProtecaoDeDados;

    /// <summary>
    /// Consulta o que a requisição pode, sem registrar. Nulo quando o token não identifica o solicitante por completo: a
    /// borda responde 401, e nenhuma interpretação de acesso se faz antes disso.
    /// </summary>
    /// <param name="incluirAdministrar">Só a leitura de pedido alheio precisa saber se administra concessões.</param>
    /// <param name="pedidoId">O pedido acessado, quando a consulta é sobre um; a trilha então nomeia o pedido.</param>
    public static async Task<AcessoDoAprovador?> ConsultarAsync(
        IVerificadorDeAcesso verificador, bool incluirAdministrar, Guid? pedidoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        ResourceContext? ResourceDe(PermissionRequirement permissao) => pedidoId is { } id
            ? ResourceContext.From(ResourceTiposAutorizacao.Pedido, permissao.Sensibilidade, resourceId: id).Value
            : null;

        DecisaoDeAcesso funcional = await verificador.AvaliarAsync(PermissaoFuncional, ResourceDe(PermissaoFuncional), cancellationToken).ConfigureAwait(false);
        if (funcional is SemSujeito)
        {
            return null;
        }

        DecisaoDeAcesso protecao = await verificador.AvaliarAsync(PermissaoProtecaoDeDados, ResourceDe(PermissaoProtecaoDeDados), cancellationToken).ConfigureAwait(false);
        DecisaoDeAcesso? administrar = incluirAdministrar
            ? await verificador.AvaliarAsync(PermissaoAdministrar, ResourceDe(PermissaoAdministrar), cancellationToken).ConfigureAwait(false)
            : null;

        return new AcessoDoAprovador(funcional, protecao, administrar);
    }

    /// <summary>
    /// Registra a decisão que governou o desfecho, permitida ou negada. Pergunta pelo acesso a uma fila ou a uma etapa
    /// tem uma permissão só, e é essa que a trilha recebe.
    /// </summary>
    public static Task RegistrarAsync(IVerificadorDeAcesso verificador, DecisaoDeAcesso decisao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        return verificador.RegistrarDesfechoAsync([decisao], IntencaoDoRegistro.Aprovador, cancellationToken);
    }

    /// <summary>
    /// Registra o desfecho quando nenhuma permissão específica o governou: as decisões permitidas que habilitaram ou,
    /// se nenhuma habilitou, as negativas, que são a tentativa real.
    /// </summary>
    public Task RegistrarDoDesfechoAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso[] todas = [DecisaoFuncional, DecisaoProtecaoDeDados, .. DecisaoAdministrar is null ? [] : new[] { DecisaoAdministrar }];

        return verificador.RegistrarDesfechoAsync(todas, IntencaoDoRegistro.Aprovador, cancellationToken);
    }

    /// <summary>
    /// Se a pessoa lista a fila, registrando a decisão que governa: a da fila pedida, ou o desfecho quando a fila é
    /// de qualquer etapa.
    /// </summary>
    public async Task<bool> PodeListarAsync(IVerificadorDeAcesso verificador, Domain.Interfaces.FilaDePedidos fila, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        switch (fila)
        {
            case Domain.Interfaces.FilaDePedidos.Gestor:
                await RegistrarAsync(verificador, DecisaoFuncional, cancellationToken).ConfigureAwait(false);
                return Funcional;
            case Domain.Interfaces.FilaDePedidos.ProtecaoDeDados:
                await RegistrarAsync(verificador, DecisaoProtecaoDeDados, cancellationToken).ConfigureAwait(false);
                return ProtecaoDeDados;
            default:
                await RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
                return Aprova;
        }
    }

    private static bool Permitida(DecisaoDeAcesso? decisao) => decisao is DecisaoPermitida;
}
