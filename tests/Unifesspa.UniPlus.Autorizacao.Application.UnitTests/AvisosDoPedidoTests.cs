namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Os avisos do pedido por situação: quem recebe, quem fica de fora e o que o corpo traz. Só personas fictícias do portal.
/// </summary>
public sealed class AvisosDoPedidoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";
    private const string PerfilNome = "Análise <de> atendimento";

    private static readonly PessoaNoToken Betina = new(new Identidade(Emissor, "betina.neves"), "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local", true);
    private static readonly PessoaNoToken Leandro = new(new Identidade(Emissor, "leandro.viana"), "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local", true);
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly Identidade Isabella = new(Emissor, "isabella.figueiredo");
    private static readonly Identidade Marlene = new(Emissor, "marlene.martins");

    private const string EmailDiogo = "diogo.bruno.souza@uniplus-test.local";
    private const string EmailIsabella = "isabellapriscilafigueiredo@uniplus-test.local";
    private const string EmailMarlene = "marlenemarciamartins@uniplus-test.local";

    [Fact(DisplayName = "O pedido que aguarda o aceite avisa só o beneficiário, com a finalidade do aceite pendente")]
    public async Task AguardandoBeneficiario_AvisaOBeneficiario()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, situacaoAnterior: null);

        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Destinatario.Should().Be(Leandro.Email);
        aviso.Finalidade.Should().Be("pedido.aceite_pendente");
    }

    [Fact(DisplayName = "O corpo traz perfil, escopo, situação e link, escapa o HTML e não traz nome, justificativa nem motivo")]
    public async Task Corpo_TrazSoOEssencial()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false, unidade: Guid.NewGuid());

        EnviarEmail aviso = (await Avisar(pedido, situacaoAnterior: null)).Single();

        string link = $"https://uniplus.test/configuracao/acessos/pedidos/{pedido.Id}";
        aviso.CorpoTexto.Should().Contain(PerfilNome).And.Contain("Escopo: CEPS").And.Contain("Aguardando o aceite do beneficiário").And.Contain(link);
        aviso.CorpoHtml.Should().Contain("&lt;de&gt;").And.NotContain("<de>").And.Contain($"href=\"{link}\"");
        foreach (string texto in new[] { aviso.CorpoTexto, aviso.CorpoHtml, aviso.Assunto })
        {
            texto.Should().NotContain(Betina.Nome!).And.NotContain(Leandro.Nome!).And.NotContain(Justificativa).And.NotContain(Betina.Email!);
        }
    }

    [Fact(DisplayName = "O escopo da instituição aparece como Instituição")]
    public async Task Corpo_EscopoInstitucional()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);

        EnviarEmail aviso = (await Avisar(pedido, situacaoAnterior: null)).Single();

        aviso.CorpoTexto.Should().Contain("Escopo: Instituição");
    }

    [Fact(DisplayName = "A fila do gestor avisa os gestores ativos e deixa de fora as partes, sem e-mail e repetidos")]
    public async Task AguardandoGestor_AvisaOsGestoresForaDasPartes()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);
        pedido.Aceitar(Leandro, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        ConcessoesComTitulares concessoes = new(PerfisDeSistema.GestorDeAcessosId,
            new TitularComEmail(Diogo, EmailDiogo),
            new TitularComEmail(Diogo, EmailDiogo.ToUpperInvariant()),
            new TitularComEmail(Isabella, EmailIsabella),
            new TitularComEmail(Betina.Identidade, Betina.Email!),
            new TitularComEmail(new Identidade(Emissor, "outro.sub"), Leandro.Email!));

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, situacaoAnterior: SituacaoDoPedido.AguardandoBeneficiario, concessoes);

        avisos.Select(a => a.Destinatario).Should().BeEquivalentTo([EmailDiogo, EmailIsabella]);
        avisos.Should().OnlyContain(a => a.Finalidade == "pedido.fila_do_gestor");
        concessoes.PerfilConsultado.Should().Be(PerfisDeSistema.GestorDeAcessosId);
    }

    [Fact(DisplayName = "A fila da proteção de dados exclui as partes e quem aprovou como gestor")]
    public async Task AguardandoProtecaoDeDados_ExcluiQuemAprovouComoGestor()
    {
        PedidoDeConcessao pedido = PedidoAprovadoPeloGestor(Diogo);
        ConcessoesComTitulares concessoes = new(PerfisDeSistema.ProtecaoDeDadosId,
            new TitularComEmail(Diogo, EmailDiogo),
            new TitularComEmail(Marlene, EmailMarlene),
            new TitularComEmail(Leandro.Identidade, Leandro.Email!));

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoGestor, concessoes);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoProtecaoDeDados);
        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Destinatario.Should().Be(EmailMarlene);
        aviso.Finalidade.Should().Be("pedido.fila_da_protecao_de_dados");
        concessoes.PerfilConsultado.Should().Be(PerfisDeSistema.ProtecaoDeDadosId);
    }

    [Fact(DisplayName = "O pedido aprovado avisa o solicitante e o beneficiário, um e-mail por endereço")]
    public async Task Aprovado_AvisaSolicitanteEBeneficiario()
    {
        PedidoDeConcessao pedido = PedidoDecidido(aprovar: true);

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoGestor);

        avisos.Select(a => a.Destinatario).Should().BeEquivalentTo([Betina.Email, Leandro.Email]);
        avisos.Should().OnlyContain(a => a.Finalidade == "pedido.aprovado");
        avisos.Select(a => a.MensagemId).Distinct().Should().HaveCount(2);
    }

    [Fact(DisplayName = "O pedido recusado avisa as duas partes sem o motivo da recusa")]
    public async Task Recusado_AvisaAsPartesSemOMotivo()
    {
        PedidoDeConcessao pedido = PedidoDecidido(aprovar: false);

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoGestor);

        avisos.Select(a => a.Destinatario).Should().BeEquivalentTo([Betina.Email, Leandro.Email]);
        avisos.Should().OnlyContain(a => a.Finalidade == "pedido.recusado" && !a.CorpoTexto.Contains("Motivo sigiloso", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O pedido para si mesmo avisa um só endereço no desfecho")]
    public async Task Aprovado_ParaSiMesmo_AvisaUmEndereco()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: true);
        pedido.Aprovar(Diogo, Avaliado(), new ContextoDaAprovacao(false), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoGestor);

        avisos.Should().ContainSingle().Which.Destinatario.Should().Be(Betina.Email);
    }

    [Fact(DisplayName = "O cancelamento avisa só o beneficiário, e o pedido sem documento ainda não chegou a ele")]
    public async Task Cancelado_AvisaSoOBeneficiarioDiferente()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);
        pedido.Cancelar(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoBeneficiario);
        IReadOnlyList<EnviarEmail> semDocumento = await Avisar(pedido, SituacaoDoPedido.AguardandoDocumento);

        EnviarEmail aviso = avisos.Should().ContainSingle().Subject;
        aviso.Destinatario.Should().Be(Leandro.Email);
        aviso.Finalidade.Should().Be("pedido.cancelado");
        semDocumento.Should().BeEmpty();
    }

    [Fact(DisplayName = "O cancelamento do pedido para si mesmo não avisa ninguém")]
    public async Task Cancelado_ParaSiMesmo_NaoAvisa()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: true);
        pedido.Cancelar(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        (await Avisar(pedido, SituacaoDoPedido.AguardandoGestor)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O pedido que aguarda o documento não avisa ninguém")]
    public async Task AguardandoDocumento_NaoAvisa()
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false, perfil: PerfilDeGovernanca());
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);

        (await Avisar(pedido, situacaoAnterior: null)).Should().BeEmpty();
    }

    [Fact(DisplayName = "O pedido sem e-mail do solicitante avisa só o beneficiário no desfecho")]
    public async Task Aprovado_SemEmailDoSolicitante_AvisaOBeneficiario()
    {
        PedidoDeConcessao pedido = PedidoDecidido(aprovar: true);
        typeof(PedidoDeConcessao).GetProperty(nameof(PedidoDeConcessao.SolicitanteEmail))!.SetValue(pedido, null);

        IReadOnlyList<EnviarEmail> avisos = await Avisar(pedido, SituacaoDoPedido.AguardandoGestor);

        avisos.Should().ContainSingle().Which.Destinatario.Should().Be(Leandro.Email);
    }

    [Fact(DisplayName = "O e-mail do solicitante é o verificado do token, em minúsculas")]
    public void Criar_GravaOEmailVerificadoDoSolicitante()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina with { Email = "Betina-DasNeves81@uniplus-test.local" },
            Leandro.Email, PerfilComum(), EscopoDaConcessao.DaInstituicao(), Justificativa,
            new Dominios(), new Catalogo(), DateTimeOffset.UtcNow).Value!;

        pedido.SolicitanteEmail.Should().Be(Betina.Email);
    }

    private static Task<IReadOnlyList<EnviarEmail>> Avisar(
        PedidoDeConcessao pedido, SituacaoDoPedido? situacaoAnterior, ConcessoesComTitulares? concessoes = null) =>
        new AvisosDoPedido(
            concessoes ?? new ConcessoesComTitulares(Guid.Empty),
            new Unidades(),
            new Link(),
            TimeProvider.System)
            .AvisarAsync(pedido, PerfilNome, situacaoAnterior, CancellationToken.None);

    private static PedidoDeConcessao Pedido(bool paraSiMesmo, Guid? unidade = null, Perfil? perfil = null)
    {
        Perfil alvo = perfil ?? PerfilComum(deUnidade: unidade is not null);
        return PedidoDeConcessao.Criar(
            Betina,
            paraSiMesmo ? null : Leandro.Email,
            alvo,
            unidade is { } id ? EscopoDaConcessao.DaUnidade(id) : EscopoDaConcessao.DaInstituicao(),
            Justificativa,
            new Dominios(),
            new Catalogo(),
            DateTimeOffset.UtcNow).Value!;
    }

    private static PedidoDeConcessao PedidoAprovadoPeloGestor(Identidade gestor)
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);
        pedido.Aceitar(Leandro, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        pedido.Aprovar(gestor, Avaliado(sensivel: true), new ContextoDaAprovacao(false), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        return pedido;
    }

    private static PedidoDeConcessao PedidoDecidido(bool aprovar)
    {
        PedidoDeConcessao pedido = Pedido(paraSiMesmo: false);
        pedido.Aceitar(Leandro, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        if (aprovar)
        {
            pedido.Aprovar(Diogo, Avaliado(), new ContextoDaAprovacao(false), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }
        else
        {
            pedido.Recusar(Diogo, "Motivo sigiloso da recusa.", Avaliado(), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        }

        return pedido;
    }

    private static PerfilAvaliado Avaliado(bool sensivel = false) =>
        new(["selecao:processos:consultar"], sensivel ? "sensivel" : "interno", sensivel, null);

    private static Perfil PerfilComum(bool deUnidade = false) =>
        Perfil.Criar(
            "Análise de atendimento",
            "Analisa os pedidos de atendimento.",
            [deUnidade ? "selecao:processos:consultar" : "geral:pedidos:consultar"],
            new Catalogo()).Value!;

    private static Perfil PerfilDeGovernanca()
    {
        Perfil perfil = PerfilComum();
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!.SetValue(perfil, TipoDeGovernanca.GestorDeAcessos);
        return perfil;
    }

    private sealed class Link : ILinkDoPedido
    {
        public string Para(Guid pedidoId) => $"https://uniplus.test/configuracao/acessos/pedidos/{pedidoId}";
    }

    private sealed class Unidades : ILeitorDeUnidade
    {
        public Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<UnidadeView?>(new UnidadeView(id, "CEPS", "ceps", "Centro de Processos Seletivos", null, "centro", false, null));
    }

    private sealed class Dominios : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => true;
    }

    private sealed class Catalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:", StringComparison.Ordinal);
    }

    private sealed class ConcessoesComTitulares(Guid perfilEsperado, params TitularComEmail[] titulares) : IConcessaoRepository
    {
        public Guid? PerfilConsultado { get; private set; }

        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(
            Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default)
        {
            PerfilConsultado = perfilId;
            return Task.FromResult<IReadOnlyList<TitularComEmail>>(perfilId == perfilEsperado ? titulares : []);
        }

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
