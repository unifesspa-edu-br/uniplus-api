namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O fluxo dos pedidos consulta o acesso sem registrar, e quem usa o resultado grava a decisão que governou o desfecho. Diogo Souza é gestor de acessos (só a etapa funcional); Paulo Nogueira não tem permissão alguma.
/// </summary>
public sealed class AcessoDoAprovadorTests
{
    private const string Funcional = "autorizacao:pedidos:aprovar-funcional";

    private const string Protecao = "autorizacao:pedidos:aprovar-protecao-de-dados";

    [Fact(DisplayName = "Consultar o acesso não grava nada: quem registra é quem sabe qual permissão governou")]
    public async Task Consultar_NaoGrava()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);

        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: true, pedidoId: null, default);

        acesso!.Funcional.Should().BeTrue();
        acesso.ProtecaoDeDados.Should().BeFalse();
        verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o pedido conhecido, a trilha nomeia o pedido: tipo e id, e dois pedidos não colapsam")]
    public async Task ComPedido_GravaOResourceDoPedido()
    {
        Guid primeiro = Guid.CreateVersion7();
        Guid segundo = Guid.CreateVersion7();
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);

        foreach (Guid pedido in new[] { primeiro, segundo })
        {
            AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: true, pedido, default))!;
            await acesso.RegistrarDoDesfechoAsync(verificador, default);
        }

        verificador.Gravados.Select(static g => g.Single()).Should().OnlyContain(r => r.ResourceTipo == "pedido-de-concessao");
        verificador.Gravados.Select(static g => g.Single().ResourceId).Should().Equal(primeiro, segundo);
        verificador.Gravados.Select(static g => g.Single().Chave).Distinct().Should().HaveCount(2);
    }

    [Fact(DisplayName = "Pedido inexistente pedido sem permissão grava as negativas sem o id da rota")]
    public async Task Inexistente_SemPermissao_GravaAsNegativasSemId()
    {
        VerificadorDeTeste verificador = new("paulo.nogueira", permitidas: []);

        await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, incluirAdministrar: true, default);

        IReadOnlyCollection<RegistroDecisaoAcesso> gravados = verificador.Gravados.Should().ContainSingle().Subject;
        gravados.Should().HaveCount(3).And.OnlyContain(r => !r.Permitido && r.ResourceId == null);
    }

    [Fact(DisplayName = "Pedido inexistente pedido por quem tem a permissão de ver não grava nada")]
    public async Task Inexistente_ComPermissao_NaoGrava()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);

        await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, incluirAdministrar: true, default);

        verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Autor não identificado recusa sem registrar acesso algum: a recusa não é de autorização")]
    public async Task AutorNaoIdentificado_NaoGrava()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);

        Unifesspa.UniPlus.Kernel.Results.Result<DecisaoDaEtapa> resultado = await DecisaoDaEtapa.PrepararAsync(
            Guid.CreateVersion7(), null!, null!, verificador, null!, decisor: null, default);

        resultado.IsFailure.Should().BeTrue();
        verificador.Gravados.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem permissão e com o id de um pedido que não existe, a recusa grava as negativas sem o id forjado")]
    public async Task SemPermissao_PedidoInexistente_GravaSemOId()
    {
        VerificadorDeTeste verificador = new("paulo.nogueira", permitidas: []);

        Unifesspa.UniPlus.Kernel.Results.Result<DecisaoDaEtapa> resultado = await DecisaoDaEtapa.PrepararAsync(
            Guid.CreateVersion7(), new PedidosInexistentes(), null!, verificador, null!, new Identidade("https://idp/x", "paulo.nogueira"), default);

        resultado.IsFailure.Should().BeTrue();
        verificador.Gravados.Should().ContainSingle().Which.Should().OnlyContain(r => !r.Permitido && r.ResourceId == null);
    }

    [Fact(DisplayName = "Quem tem só a etapa funcional e pede a fila da proteção de dados é recusado e grava a negativa dela, não um permitido")]
    public async Task FilaDeOutraEtapa_GravaANegativaDaFilaPedida()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);
        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, pedidoId: null, default))!;

        bool pode = await acesso.PodeListarAsync(verificador, FilaDePedidos.ProtecaoDeDados, default);

        pode.Should().BeFalse();
        RegistroDecisaoAcesso gravado = verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        gravado.Permissao.Should().Be(Protecao);
        gravado.Permitido.Should().BeFalse();
    }

    [Fact(DisplayName = "Quem tem a etapa funcional e pede a fila do gestor grava a decisão permitida dela")]
    public async Task FilaDaPropriaEtapa_GravaAPermitida()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional]);
        AcessoDoAprovador acesso = (await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, pedidoId: null, default))!;

        bool pode = await acesso.PodeListarAsync(verificador, FilaDePedidos.Gestor, default);

        pode.Should().BeTrue();
        RegistroDecisaoAcesso gravado = verificador.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        gravado.Permissao.Should().Be(Funcional);
        gravado.Permitido.Should().BeTrue();
    }

    [Fact(DisplayName = "O desfecho sem permissão específica grava a permitida que habilitou, ou as negativas se nenhuma habilitou")]
    public async Task Desfecho_GravaAPermitidaOuAsNegativas()
    {
        VerificadorDeTeste comUma = new("diogo.souza", permitidas: [Funcional]);
        VerificadorDeTeste semNenhuma = new("paulo.nogueira", permitidas: []);

        await (await AcessoDoAprovador.ConsultarAsync(comUma, incluirAdministrar: false, pedidoId: null, default))!.RegistrarDoDesfechoAsync(comUma, default);
        await (await AcessoDoAprovador.ConsultarAsync(semNenhuma, incluirAdministrar: false, pedidoId: null, default))!.RegistrarDoDesfechoAsync(semNenhuma, default);

        comUma.Gravados.Should().ContainSingle().Subject.Should().ContainSingle().Which.Permitido.Should().BeTrue();
        semNenhuma.Gravados.Should().ContainSingle().Subject.Should().HaveCount(2).And.OnlyContain(r => !r.Permitido);
    }

    [Fact(DisplayName = "Com as duas etapas permitidas ao mesmo tempo, o desfecho grava as duas permissões que habilitaram a aprovação")]
    public async Task Desfecho_DuasPermitidas_GravaAsDuas()
    {
        VerificadorDeTeste verificador = new("diogo.souza", permitidas: [Funcional, Protecao]);

        await (await AcessoDoAprovador.ConsultarAsync(verificador, incluirAdministrar: false, pedidoId: null, default))!
            .RegistrarDoDesfechoAsync(verificador, default);

        verificador.Gravados.Should().ContainSingle().Subject.Select(static r => r.Permissao).Should().BeEquivalentTo([Funcional, Protecao]);
    }

    private sealed class PedidosInexistentes : IPedidoDeConcessaoRepository
    {
        public Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PedidoDeConcessao?>(null);

        public Task<Guid?> ObterPerfilIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<IReadOnlyList<PedidoDeConcessao>> ListarAtivosDoPerfilParaAlterarAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PedidoDeConcessao?>(null);

        public Task<PedidoDeConcessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PedidoDeConcessao?>(null);

        public Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
            FiltroDePedidos filtro, string? afterSortKey, Guid? afterId, int limit, Kernel.Pagination.PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PedidoDeConcessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AdicionarAsync(PedidoDeConcessao entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Atualizar(PedidoDeConcessao entity) => throw new NotSupportedException();

        public void Remover(PedidoDeConcessao entity) => throw new NotSupportedException();
    }

    private sealed class VerificadorDeTeste(string sub, string[] permitidas) : IVerificadorDeAcesso
    {
        public List<IReadOnlyCollection<RegistroDecisaoAcesso>> Gravados { get; } = [];

        public Task<DecisaoDeAcesso> AvaliarAsync(
            PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default)
        {
            ResourceContext alvo = resource ?? ResourceContext.From(ResourceTipos.Operacao, requisito.Sensibilidade).Value!;
            AuthorizationSubject sujeito = AuthorizationSubject.From(
                UsuarioRef.From("https://idp.exemplo/realms/unifesspa", sub).Value!, "jti-teste").Value!;
            AuthorizationDecision decisao = permitidas.Contains(requisito.Permissao)
                ? AuthorizationDecision.Permitir(EffectiveGrant.From(requisito.Permissao, FonteGrant.Concessao, escopoInstitucional: true).Value!)
                : AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel);
            AuthorizationRequestContext contexto = AuthorizationRequestContext.From("req-1", DateTimeOffset.UtcNow, OrigemRequisicao.Api).Value!;

            return Task.FromResult(DecisaoDeAcesso.Tomada(
                requisito, alvo, RegistroDecisaoAcesso.De(sujeito, requisito, alvo, contexto, decisao)));
        }

        // O que o verificador real gravaria: a política de registro aplicada às decisões do desfecho.
        public Task RegistrarDesfechoAsync(
            IReadOnlyCollection<DecisaoDeAcesso> decisoes, IntencaoDoRegistro intencao, CancellationToken cancellationToken = default)
        {
            Gravados.Add(PoliticaDeRegistroDeAcesso.Desfecho(decisoes, intencao));
            return Task.CompletedTask;
        }

        public Task<ResultadoDoAcesso> ExigirAsync(PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<DecisaoDeAcesso>> AvaliarEmAlgumaUnidadeAsync(PermissionRequirement requisito, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResultadoDoAcesso> PodeAsync(PermissionRequirement requisito, ResourceContext? resource = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RegistrarParaCadaAsync(
            DecisaoPermitida permitida, string resourceTipo, IReadOnlyCollection<Guid> resourceIds, bool resourceEhInscricao = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
