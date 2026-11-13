namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que o solicitante da requisição pode fazer no fluxo dos pedidos: decidir cada etapa, administrar concessões e
/// consultar os pedidos sem decidir, como o Encarregado de dados. Cada capacidade é avaliada pelo ponto de decisão único,
/// por permissão do catálogo, sem registrar: quem usa o resultado grava o que governou o desfecho
/// (<see cref="RegistrarAsync"/>, <see cref="RegistrarDoDesfechoAsync"/> e, para os pedidos exibidos,
/// <see cref="RegistrarLeituraAsync"/>), para que a trilha traga a decisão real e não uma negativa falsa de uma permissão
/// que a pessoa nem precisava ter.
/// </summary>
internal sealed record AcessoDoAprovador(
    DecisaoDeAcesso DecisaoFuncional,
    DecisaoDeAcesso DecisaoProtecaoDeDados,
    DecisaoDeAcesso? DecisaoAdministrar,
    DecisaoDeAcesso? DecisaoConsultar)
{
    /// <summary>
    /// O código que identifica no registro de acesso a leitura que a parte faz do próprio pedido sem a consulta dos
    /// pedidos. Fica fora do catálogo de permissões: não entra em perfil, não se concede, não é aceito em pedido e não
    /// decide nada (ADR-0166).
    /// </summary>
    internal const string CodigoDaLeituraPorTitularidade = "autorizacao:pedidos:ler-proprios";

    private static readonly PermissionRequirement PermissaoFuncional = Requisito(PermissaoAutorizacao.PedidosAprovarFuncional);

    private static readonly PermissionRequirement PermissaoProtecaoDeDados = Requisito(PermissaoAutorizacao.PedidosAprovarProtecaoDeDados);

    private static readonly PermissionRequirement PermissaoAdministrar = Requisito(PermissaoAutorizacao.ConcessoesAdministrar);

    private static readonly PermissionRequirement PermissaoConsultar = Requisito(PermissaoAutorizacao.PedidosConsultar);

    // A leitura por titularidade tem a sensibilidade da consulta dos pedidos: a parte lê o nome, o e-mail, o texto livre
    // e o anexo da outra parte, e o registro classifica o dado lido do mesmo modo nos dois caminhos.
    private static readonly PermissionRequirement LeituraPorTitularidade =
        PermissionRequirement.From(CodigoDaLeituraPorTitularidade, PermissaoConsultar.Sensibilidade).Value!;

    /// <summary>Decide a etapa do gestor de acessos.</summary>
    public bool Funcional => DecisaoFuncional is DecisaoPermitida;

    /// <summary>Decide a etapa da proteção de dados.</summary>
    public bool ProtecaoDeDados => DecisaoProtecaoDeDados is DecisaoPermitida;

    /// <summary>Administra concessões.</summary>
    public bool Administrar => DecisaoAdministrar is DecisaoPermitida;

    /// <summary>Consulta os pedidos sem decidir nenhuma etapa.</summary>
    public bool Consulta => DecisaoConsultar is DecisaoPermitida;

    /// <summary>Decide alguma das duas etapas.</summary>
    public bool Aprova => Funcional || ProtecaoDeDados;

    /// <summary>Vê o pedido de qualquer pessoa: decide uma etapa, administra concessões ou consulta os pedidos.</summary>
    public bool VeTodos => Aprova || Administrar || Consulta;

    private DecisaoDeAcesso[] Avaliadas =>
        [DecisaoFuncional, DecisaoProtecaoDeDados, .. new[] { DecisaoAdministrar, DecisaoConsultar }.OfType<DecisaoDeAcesso>()];

    /// <summary>A permissão da etapa que o pedido aguarda.</summary>
    public bool Decide(EtapaDoPedido etapa) => etapa == EtapaDoPedido.Gestor ? Funcional : ProtecaoDeDados;

    /// <summary>A decisão da permissão da etapa que o pedido aguarda.</summary>
    public DecisaoDeAcesso DecisaoDaEtapa(EtapaDoPedido etapa) =>
        etapa == EtapaDoPedido.Gestor ? DecisaoFuncional : DecisaoProtecaoDeDados;

    /// <summary>
    /// Consulta o que a requisição pode, sem registrar. Nulo quando o token não identifica o solicitante por completo: a
    /// borda responde 401, e nenhuma interpretação de acesso se faz antes disso.
    /// </summary>
    /// <param name="finalidade">Para que se consulta: define quais permissões entram na avaliação.</param>
    /// <param name="pedidoId">O pedido acessado, quando a consulta é sobre um; a trilha então nomeia o pedido.</param>
    public static async Task<AcessoDoAprovador?> ConsultarAsync(
        IVerificadorDeAcesso verificador, FinalidadeDoAcesso finalidade, Guid? pedidoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso funcional = await AvaliarAsync(verificador, PermissaoFuncional, pedidoId, cancellationToken).ConfigureAwait(false);
        if (funcional is SemSujeito)
        {
            return null;
        }

        return new AcessoDoAprovador(
            funcional,
            await AvaliarAsync(verificador, PermissaoProtecaoDeDados, pedidoId, cancellationToken).ConfigureAwait(false),
            finalidade == FinalidadeDoAcesso.Leitura ? await AvaliarAsync(verificador, PermissaoAdministrar, pedidoId, cancellationToken).ConfigureAwait(false) : null,
            finalidade == FinalidadeDoAcesso.Decisao ? null : await AvaliarAsync(verificador, PermissaoConsultar, pedidoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Registra as negativas de quem pediu um pedido que não existe sem a permissão que o veria. A decisão é tomada sem
    /// o id da rota, que é forjável e não existe: a trilha reconhece a sondagem pela ausência do id, e a resposta é a mesma
    /// de quem não tem a permissão. Quem tem a permissão errou o link, e nada se registra.
    /// </summary>
    /// <returns>
    /// A identidade incompleta quando o token não identifica o solicitante por completo, sem registro, como no pedido
    /// existente, para que a resposta não revele se o pedido existe; nulo nos demais casos.
    /// </returns>
    public static async Task<DomainError?> RegistrarNegativasDoInexistenteAsync(
        IVerificadorDeAcesso verificador, FinalidadeDoAcesso finalidade, CancellationToken cancellationToken)
    {
        AcessoDoAprovador? acesso = await ConsultarAsync(verificador, finalidade, pedidoId: null, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return PedidoHandlerErros.IdentidadeIncompleta();
        }

        if (!acesso.Ve(finalidade))
        {
            await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// A decisão que governa a leitura de quem é parte do pedido, sem gravar: a da consulta dos pedidos, quando a pessoa a
    /// tem, como o Encarregado de dados, para que a leitura entre na trilha por ela (UNI-REQ-0372). Nula para quem não tem a
    /// consulta, que lê os próprios pedidos por titularidade e tem a leitura registrada com essa origem (ADR-0166). A
    /// pergunta é de capacidade: a negativa não se grava. Sem sujeito na decisão, a falha é a identidade incompleta, e a
    /// leitura não sai, porque não se sabe por qual origem registrá-la.
    /// </summary>
    /// <param name="pedidoId">O pedido lido, ou nulo na fila dos próprios pedidos.</param>
    public static async Task<Result<DecisaoPermitida?>> GovernanteDaLeituraDaParteAsync(
        IVerificadorDeAcesso verificador, Guid? pedidoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso consulta = await AvaliarAsync(verificador, PermissaoConsultar, pedidoId, cancellationToken).ConfigureAwait(false);

        return consulta is SemSujeito
            ? Result<DecisaoPermitida?>.Failure(PedidoHandlerErros.IdentidadeIncompleta())
            : Result<DecisaoPermitida?>.Success(consulta as DecisaoPermitida);
    }

    /// <summary>
    /// Registra, antes da entrega, a leitura de cada pedido exibido: pela decisão permitida que governou a leitura ou, sem
    /// ela, por titularidade. Só a leitura de quem é parte chega sem decisão (<see cref="GovernanteDaLeituraDaParteAsync"/>):
    /// as filas que exigem permissão são recusadas antes. Sem pedido exibido, nada se grava e o registro não é consultado.
    /// </summary>
    /// <returns>Nulo com a leitura registrada, ou a identidade incompleta quando o token não identifica a pessoa.</returns>
    /// <exception cref="RegistroDeAcessoNaoGravadoException">O registro não foi gravado, e a leitura não sai.</exception>
    public static async Task<DomainError?> RegistrarLeituraAsync(
        IVerificadorDeAcesso verificador, DecisaoPermitida? governante, IReadOnlyCollection<Guid> pedidos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(pedidos);

        return (pedidos.Count, governante) switch
        {
            (0, _) => null,
            (_, null) => await RegistrarPorTitularidadeAsync(verificador, pedidos, cancellationToken).ConfigureAwait(false),
            (_, { } permitida) => await RegistrarPelaDecisaoAsync(verificador, permitida, pedidos, cancellationToken).ConfigureAwait(false),
        };
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

        return verificador.RegistrarDesfechoAsync(Avaliadas, IntencaoDoRegistro.Aprovador, cancellationToken);
    }

    /// <summary>
    /// Autoriza a fila, registrando a decisão que governa: a da etapa da fila pedida, que só quem decide a etapa lista, ou
    /// o desfecho quando a fila é a de todos, que quem consulta os pedidos também lista. Devolve a decisão permitida que
    /// governou, da qual sai o registro de cada pedido exibido, ou nulo quando a fila é recusada.
    /// </summary>
    public async Task<DecisaoPermitida?> AutorizarFilaAsync(IVerificadorDeAcesso verificador, FilaDePedidos fila, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verificador);

        DecisaoDeAcesso[] governantes = fila switch
        {
            FilaDePedidos.Gestor => [DecisaoFuncional],
            FilaDePedidos.ProtecaoDeDados => [DecisaoProtecaoDeDados],
            _ => Avaliadas,
        };
        await verificador.RegistrarDesfechoAsync(governantes, IntencaoDoRegistro.Aprovador, cancellationToken).ConfigureAwait(false);

        return governantes.OfType<DecisaoPermitida>().FirstOrDefault();
    }

    private static async Task<DomainError?> RegistrarPelaDecisaoAsync(
        IVerificadorDeAcesso verificador, DecisaoPermitida governante, IReadOnlyCollection<Guid> pedidos, CancellationToken cancellationToken)
    {
        await verificador.RegistrarParaCadaAsync(
            governante, ResourceTiposAutorizacao.Pedido, pedidos, cancellationToken: cancellationToken).ConfigureAwait(false);

        return null;
    }

    private static async Task<DomainError?> RegistrarPorTitularidadeAsync(
        IVerificadorDeAcesso verificador, IReadOnlyCollection<Guid> pedidos, CancellationToken cancellationToken)
    {
        ResultadoDoAcesso registrada = await verificador.RegistrarLeituraPorTitularidadeAsync(
            LeituraPorTitularidade, ResourceTiposAutorizacao.Pedido, pedidos, cancellationToken).ConfigureAwait(false);

        return registrada == ResultadoDoAcesso.IdentidadeIncompleta ? PedidoHandlerErros.IdentidadeIncompleta() : null;
    }

    private bool Ve(FinalidadeDoAcesso finalidade) => finalidade == FinalidadeDoAcesso.Decisao ? Aprova : VeTodos;

    private static Task<DecisaoDeAcesso> AvaliarAsync(
        IVerificadorDeAcesso verificador, PermissionRequirement permissao, Guid? pedidoId, CancellationToken cancellationToken)
    {
        ResourceContext? resource = pedidoId is { } id
            ? ResourceContext.From(ResourceTiposAutorizacao.Pedido, permissao.Sensibilidade, resourceId: id).Value
            : null;

        return verificador.AvaliarAsync(permissao, resource, cancellationToken);
    }

    private static PermissionRequirement Requisito(PermissaoAutorizacao permissao) =>
        CatalogoDePermissoes<PermissaoAutorizacao>.Obter(permissao).Requisito;
}
