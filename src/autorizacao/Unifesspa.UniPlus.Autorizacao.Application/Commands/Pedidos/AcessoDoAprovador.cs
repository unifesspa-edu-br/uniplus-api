namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// O que o solicitante da requisição pode fazer no fluxo dos pedidos: decidir cada etapa e administrar concessões.
/// Cada capacidade é conferida pelo ponto de decisão único, por permissão do catálogo.
/// </summary>
internal sealed record AcessoDoAprovador(bool Funcional, bool ProtecaoDeDados, bool Administrar)
{
    private static readonly PermissionRequirement PermissaoFuncional =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PedidosAprovarFuncional).Requisito;

    private static readonly PermissionRequirement PermissaoProtecaoDeDados =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.PedidosAprovarProtecaoDeDados).Requisito;

    private static readonly PermissionRequirement PermissaoAdministrar =
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(PermissaoAutorizacao.ConcessoesAdministrar).Requisito;

    /// <summary>Decide alguma das duas etapas.</summary>
    public bool Aprova => Funcional || ProtecaoDeDados;

    /// <summary>Vê o pedido de qualquer pessoa: decide uma etapa ou administra concessões.</summary>
    public bool VeTodos => Aprova || Administrar;

    /// <summary>A permissão da etapa que o pedido aguarda.</summary>
    public bool Decide(Domain.Enums.EtapaDoPedido etapa) =>
        etapa == Domain.Enums.EtapaDoPedido.Gestor ? Funcional : ProtecaoDeDados;

    /// <summary>
    /// Consulta o que a requisição pode. Nulo quando o token não identifica o solicitante por completo: a borda responde
    /// 401, e nenhuma interpretação de acesso se faz antes disso.
    /// </summary>
    /// <param name="incluirAdministrar">Só a leitura de pedido alheio precisa saber se administra concessões.</param>
    public static async Task<AcessoDoAprovador?> ConsultarAsync(
        IVerificadorDeAcesso verificador, bool incluirAdministrar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        ResultadoDoAcesso funcional = await verificador.VerificarAsync(PermissaoFuncional, null, cancellationToken).ConfigureAwait(false);
        if (funcional == ResultadoDoAcesso.IdentidadeIncompleta)
        {
            return null;
        }

        ResultadoDoAcesso protecao = await verificador.VerificarAsync(PermissaoProtecaoDeDados, null, cancellationToken).ConfigureAwait(false);
        ResultadoDoAcesso administrar = incluirAdministrar
            ? await verificador.VerificarAsync(PermissaoAdministrar, null, cancellationToken).ConfigureAwait(false)
            : ResultadoDoAcesso.Negado;

        return new AcessoDoAprovador(
            funcional == ResultadoDoAcesso.Permitido,
            protecao == ResultadoDoAcesso.Permitido,
            administrar == ResultadoDoAcesso.Permitido);
    }
}
