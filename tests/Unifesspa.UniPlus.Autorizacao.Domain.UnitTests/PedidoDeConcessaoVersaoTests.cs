namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Apoio;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

public sealed class PedidoDeConcessaoVersaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";
    private const string Consultar = "selecao:processos:consultar";
    private const string Analisar = "selecao:processos:analisar";

    private static readonly DateTimeOffset Agora = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Troca = Agora.AddHours(2);
    private static readonly FakeCatalogo Catalogo = new();
    private static readonly FakeDominios Dominios = new();
    private static readonly ContextoDaAprovacao Contexto = new(HaRepresentanteVigente: false);
    private static readonly PessoaNoToken Betina = Pessoa("betina.neves", "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local");
    private static readonly PessoaNoToken Leandro = Pessoa("leandro.viana", "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local");
    private static readonly Identidade Diogo = new(Emissor, "diogo.souza");
    private static readonly PerfilAvaliado Sensivel = new([Analisar, Consultar], "sensivel", true, null);

    [Fact(DisplayName = "O pedido grava a versão em vigor quando foi criado")]
    public void Criar_GravaAVersaoVigente()
    {
        Perfil perfil = PerfilNaVersao1();

        PedidoDeConcessao pedido = Criar(perfil, paraSiMesmo: true);

        pedido.VersaoId.Should().Be(perfil.VersaoVigente!.Id);
        pedido.TrocasDeVersao.Should().BeEmpty();
    }

    [Fact(DisplayName = "O pedido de perfil de governança não tem versão")]
    public void Criar_Governanca_SemVersao()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca(
            "Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], Catalogo);

        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, null, governanca, EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;

        pedido.VersaoId.Should().BeNull();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido que aguarda o beneficiário só passa para a versão nova")]
    public void AoEntrarEmVigor_SemAcrescimo_AguardandoBeneficiario_TrocaAVersao()
    {
        PedidoDeConcessao pedido = Criar(PerfilNaVersao1(), paraSiMesmo: false);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);

        VersaoDoPerfil nova = NovaVersao(comAcrescimo: false);

        pedido.AoEntrarEmVigor(nova, 1, comAcrescimo: false, Troca).Should().Be(DesfechoDaTrocaDeVersao.VersaoAtualizada);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedido.Beneficiario.Should().BeNull();
        pedido.VersaoId.Should().Be(nova.Id);
        UnicaTroca(pedido, 1, nova.Numero, DesfechoDaTrocaDeVersao.VersaoAtualizada);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido que aguarda o gestor só passa para a versão nova")]
    public void AoEntrarEmVigor_SemAcrescimo_AguardandoGestor_TrocaAVersao()
    {
        PedidoDeConcessao pedido = Criar(PerfilNaVersao1(), paraSiMesmo: true);
        VersaoDoPerfil nova = NovaVersao(comAcrescimo: false);

        pedido.AoEntrarEmVigor(nova, 1, comAcrescimo: false, Troca).Should().Be(DesfechoDaTrocaDeVersao.VersaoAtualizada);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.VersaoId.Should().Be(nova.Id);
        pedido.Decisoes.Should().BeEmpty();
        UnicaTroca(pedido, 1, nova.Numero, DesfechoDaTrocaDeVersao.VersaoAtualizada);
    }

    [Fact(DisplayName = "Sem acréscimo, o pedido que espera a proteção de dados volta ao gestor e perde o prazo definido")]
    public void AoEntrarEmVigor_SemAcrescimo_AguardandoProtecaoDeDados_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = AguardandoProtecaoDeDados(Agora.AddDays(90));
        VersaoDoPerfil nova = NovaVersao(comAcrescimo: false);

        pedido.AoEntrarEmVigor(nova, 1, comAcrescimo: false, Troca).Should().Be(DesfechoDaTrocaDeVersao.VoltouAoGestor);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.ValidaAte.Should().BeNull();
        pedido.VersaoId.Should().Be(nova.Id);
        pedido.Decisoes.Should().ContainSingle("a aprovação já dada fica na trilha");
        UnicaTroca(pedido, 1, nova.Numero, DesfechoDaTrocaDeVersao.VoltouAoGestor);
    }

    [Fact(DisplayName = "O pedido de governança que espera o documento só passa para a versão nova e continua esperando")]
    public void AoEntrarEmVigor_SemAcrescimo_AguardandoDocumento_TrocaAVersao()
    {
        Perfil governanca = PerfisDeTeste.DeGovernanca(
            "Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], Catalogo);
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, null, governanca, EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;
        VersaoDoPerfil nova = NovaVersao(comAcrescimo: false);

        pedido.AoEntrarEmVigor(nova, 1, comAcrescimo: false, Troca).Should().Be(DesfechoDaTrocaDeVersao.VersaoAtualizada);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
    }

    [Theory(DisplayName = "Com acréscimo, o pedido ativo é devolvido, encerrado e sem mudar de versão")]
    [InlineData(SituacaoDoPedido.AguardandoBeneficiario)]
    [InlineData(SituacaoDoPedido.AguardandoGestor)]
    [InlineData(SituacaoDoPedido.AguardandoProtecaoDeDados)]
    public void AoEntrarEmVigor_ComAcrescimo_Devolve(SituacaoDoPedido situacao)
    {
        PedidoDeConcessao pedido = PedidoEm(situacao, out Guid versaoDoPedido);
        VersaoDoPerfil nova = NovaVersao(comAcrescimo: true);

        pedido.AoEntrarEmVigor(nova, 1, comAcrescimo: true, Troca).Should().Be(DesfechoDaTrocaDeVersao.Devolvido);

        pedido.Situacao.Should().Be(SituacaoDoPedido.Devolvido);
        pedido.EncerradoEm.Should().Be(Troca);
        pedido.Ativo.Should().BeFalse();
        pedido.VersaoId.Should().Be(versaoDoPedido, "o pedido nunca chegou a seguir a versão nova");
        UnicaTroca(pedido, 1, nova.Numero, DesfechoDaTrocaDeVersao.Devolvido);
    }

    [Theory(DisplayName = "O pedido que já terminou fica como está e não grava troca")]
    [InlineData(SituacaoDoPedido.Aprovado)]
    [InlineData(SituacaoDoPedido.Recusado)]
    [InlineData(SituacaoDoPedido.Cancelado)]
    [InlineData(SituacaoDoPedido.Devolvido)]
    public void AoEntrarEmVigor_PedidoTerminal_Inalterado(SituacaoDoPedido terminal)
    {
        PedidoDeConcessao pedido = Terminal(terminal);
        Guid? versao = pedido.VersaoId;
        DateTimeOffset? encerradoEm = pedido.EncerradoEm;
        int trocasAntes = pedido.TrocasDeVersao.Count;

        pedido.AoEntrarEmVigor(NovaVersao(comAcrescimo: true), 1, comAcrescimo: true, Troca)
            .Should().Be(DesfechoDaTrocaDeVersao.Inalterado);

        pedido.Situacao.Should().Be(terminal);
        pedido.VersaoId.Should().Be(versao);
        pedido.EncerradoEm.Should().Be(encerradoEm);
        pedido.TrocasDeVersao.Should().HaveCount(trocasAntes);
    }

    [Fact(DisplayName = "A conferência da versão aceita o pedido que segue a versão em vigor")]
    public void ExigirVersaoEmVigorDe_VersaoVigente_Aceita()
    {
        Perfil perfil = PerfilNaVersao1();
        PedidoDeConcessao pedido = Criar(perfil, paraSiMesmo: true);

        FluentActions.Invoking(() => pedido.ExigirVersaoEmVigorDe(perfil)).Should().NotThrow();
    }

    [Fact(DisplayName = "O pedido que segue versão que já saiu de vigor é defeito, não situação de negócio")]
    public void ExigirVersaoEmVigorDe_OutraVersao_Lanca()
    {
        Perfil perfil = PerfilNaVersao1();
        PedidoDeConcessao pedido = Criar(perfil, paraSiMesmo: true);
        PerfisDeTeste.NovaVersaoVigente(perfil, [Consultar, Analisar], Catalogo);

        FluentActions.Invoking(() => pedido.ExigirVersaoEmVigorDe(perfil)).Should().Throw<InvalidOperationException>();
    }

    private static void UnicaTroca(PedidoDeConcessao pedido, int anterior, int nova, DesfechoDaTrocaDeVersao desfecho)
    {
        TrocaDeVersaoDoPedido troca = pedido.TrocasDeVersao.Should().ContainSingle().Subject;
        troca.PedidoId.Should().Be(pedido.Id);
        troca.VersaoAnterior.Should().Be(anterior);
        troca.VersaoNova.Should().Be(nova);
        troca.Desfecho.Should().Be(desfecho);
        troca.Instante.Should().Be(Troca);
    }

    private static PedidoDeConcessao PedidoEm(SituacaoDoPedido situacao, out Guid versaoDoPedido)
    {
        Perfil perfil = PerfilNaVersao1();
        versaoDoPedido = perfil.VersaoVigente!.Id;

        return situacao switch
        {
            SituacaoDoPedido.AguardandoBeneficiario => Criar(perfil, paraSiMesmo: false),
            SituacaoDoPedido.AguardandoGestor => Criar(perfil, paraSiMesmo: true),
            _ => AguardandoProtecaoDeDados(null, perfil),
        };
    }

    private static PedidoDeConcessao AguardandoProtecaoDeDados(DateTimeOffset? validaAte, Perfil? perfil = null)
    {
        PedidoDeConcessao pedido = Criar(perfil ?? PerfilNaVersao1(), paraSiMesmo: true);
        pedido.Aprovar(Diogo, Sensivel, Contexto, Agora, validaAte).Value.Should().Be(DesfechoDaAprovacao.AguardaProtecaoDeDados);

        return pedido;
    }

    private static PedidoDeConcessao Terminal(SituacaoDoPedido terminal)
    {
        PedidoDeConcessao pedido = Criar(PerfilNaVersao1(), paraSiMesmo: true);
        PerfilAvaliado interno = new([Consultar], "interno", false, null);
        _ = terminal switch
        {
            SituacaoDoPedido.Aprovado => pedido.Aprovar(Diogo, interno, Contexto, Agora).IsSuccess,
            SituacaoDoPedido.Recusado => pedido.Recusar(Diogo, "Sem adequação funcional.", interno, Agora).IsSuccess,
            SituacaoDoPedido.Cancelado => pedido.Cancelar(Agora).IsSuccess,
            _ => EncerrarDevolvido(pedido),
        };

        return pedido;
    }

    private static bool EncerrarDevolvido(PedidoDeConcessao pedido) =>
        pedido.AoEntrarEmVigor(NovaVersao(comAcrescimo: true), 1, comAcrescimo: true, Agora) == DesfechoDaTrocaDeVersao.Devolvido;

    private static VersaoDoPerfil NovaVersao(bool comAcrescimo)
    {
        Perfil perfil = PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", [Consultar], Catalogo);
        PerfisDeTeste.NovaVersaoVigente(perfil, comAcrescimo ? [Consultar, Analisar] : [Consultar], Catalogo);

        return perfil.VersaoVigente!;
    }

    private static Perfil PerfilNaVersao1() =>
        PerfisDeTeste.Vigente("Acompanhamento", "Acompanha os atendimentos.", [Consultar], Catalogo);

    private static PedidoDeConcessao Criar(Perfil perfil, bool paraSiMesmo) =>
        PedidoDeConcessao.Criar(
            Betina,
            paraSiMesmo ? null : Leandro.Email,
            perfil,
            EscopoDaConcessao.DaUnidade(Guid.NewGuid()),
            Justificativa,
            Dominios,
            Catalogo,
            Agora).Value!;

    private static PessoaNoToken Pessoa(string sub, string nome, string email) =>
        new(new Identidade(Emissor, sub), nome, email, true);

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }

    private sealed class FakeDominios : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => string.Equals(dominio, "uniplus-test.local", StringComparison.OrdinalIgnoreCase);
    }
}
