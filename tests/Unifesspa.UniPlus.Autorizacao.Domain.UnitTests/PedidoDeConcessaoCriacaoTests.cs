namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A criação do pedido e o aceite do beneficiário.</summary>
public sealed class PedidoDeConcessaoCriacaoTests : PedidoDeConcessaoTestBase
{
    [Fact(DisplayName = "Pedido para si mesmo, sem e-mail informado, nasce aguardando o gestor com a identidade do token")]
    public void Criar_ParaSiMesmo_NasceAguardandoOGestor()
    {
        PedidoDeConcessao pedido = Criar(Leandro, null);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Beneficiario.Should().Be(Leandro.Identidade);
        pedido.BeneficiarioNome.Should().Be("Leandro Caleb Viana");
        pedido.BeneficiarioEmail.Should().Be("leandro-viana84@uniplus-test.local");
        pedido.BeneficiarioAceitouEm.Should().BeNull();
        pedido.Solicitante.Should().Be(Leandro.Identidade);
    }

    [Fact(DisplayName = "Informar o próprio e-mail verificado equivale a pedir para si mesmo")]
    public void Criar_ComOProprioEmail_ECriadoParaSiMesmo()
    {
        PedidoDeConcessao pedido = Criar(Leandro, " Leandro-Viana84@uniplus-test.local ");

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Beneficiario.Should().Be(Leandro.Identidade);
    }

    [Fact(DisplayName = "Pedido para si mesmo exige o e-mail verificado da sessão")]
    public void Criar_ParaSiMesmo_SemEmailVerificado_Recusa() =>
        Codigos(CriarResultado(Leandro with { EmailVerificado = false }, null))
            .Should().Equal(PedidoErrorCodes.BeneficiarioInvalido);

    [Fact(DisplayName = "Pedido para si mesmo exige e-mail institucional e nome completo na sessão")]
    public void Criar_ParaSiMesmo_ComEmailExternoOuSemNome_Recusa()
    {
        Codigos(CriarResultado(Leandro with { Email = "leandro@gmail.com" }, null))
            .Should().Equal(PedidoErrorCodes.BeneficiarioInvalido);
        Codigos(CriarResultado(Leandro with { Nome = null }, null))
            .Should().Equal(PedidoErrorCodes.BeneficiarioSemNome);
    }

    [Fact(DisplayName = "Pedido para outra pessoa nasce aguardando o aceite, sem identidade do beneficiário")]
    public void Criar_ParaOutraPessoa_NasceAguardandoOBeneficiario()
    {
        PedidoDeConcessao pedido = Criar(Betina, "Leandro-Viana84@uniplus-test.local");

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedido.Beneficiario.Should().BeNull();
        pedido.BeneficiarioNome.Should().BeNull();
        pedido.BeneficiarioEmail.Should().Be("leandro-viana84@uniplus-test.local");
        pedido.Solicitante.Should().Be(Betina.Identidade);
    }

    [Theory(DisplayName = "O beneficiário de outra pessoa é identificado só por e-mail institucional válido")]
    [InlineData("leandro@gmail.com")]
    [InlineData("sem-arroba")]
    public void Criar_ParaOutraPessoa_ComEmailInvalido_Recusa(string email) =>
        Codigos(CriarResultado(Betina, email))
            .Should().Equal(PedidoErrorCodes.BeneficiarioInvalido);

    [Fact(DisplayName = "Quem pede para outra pessoa precisa de e-mail institucional verificado, mesmo com o beneficiário válido")]
    public void Criar_ParaOutraPessoa_ComSolicitanteNaoInstitucional_Recusa()
    {
        Codigos(CriarResultado(Betina with { Email = "betina@gmail.com" }, Leandro.Email))
            .Should().Equal(PedidoErrorCodes.SolicitanteNaoInstitucional);
        Codigos(CriarResultado(Betina with { EmailVerificado = false }, Leandro.Email))
            .Should().Equal(PedidoErrorCodes.SolicitanteNaoInstitucional);
        Codigos(CriarResultado(Betina with { Email = null }, Leandro.Email))
            .Should().Equal(PedidoErrorCodes.SolicitanteNaoInstitucional);
    }

    [Fact(DisplayName = "Acumula as violações independentes: perfil inativo, escopo, justificativa e beneficiário")]
    public void Criar_ViolacoesIndependentes_Acumula()
    {
        Perfil perfil = PerfilComum();
        PerfisDeTeste.Desativar(perfil, Catalogo);

        Result<PedidoDeConcessao> resultado = PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro@gmail.com"), perfil, EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido("curta"), Dominios, Catalogo, Agora);

        Codigos(resultado).Should().BeEquivalentTo(
            PedidoErrorCodes.PerfilInativo,
            PedidoErrorCodes.EscopoInvalido,
            PedidoErrorCodes.JustificativaInvalida,
            PedidoErrorCodes.BeneficiarioInvalido);
    }

    [Theory(DisplayName = "Justificativa em branco, curta, longa ou com caractere nulo é recusada")]
    [InlineData(null)]
    [InlineData("         ")]
    [InlineData("curta")]
    [InlineData("Justificativa com caractere\0 nulo")]
    public void Criar_JustificativaInvalida_Recusa(string? justificativa) =>
        Codigos(PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro-viana84@uniplus-test.local"), PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(justificativa), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.JustificativaInvalida);

    [Fact(DisplayName = "Justificativa com mais de 2000 caracteres é recusada")]
    public void Criar_JustificativaLonga_Recusa() =>
        Codigos(PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro-viana84@uniplus-test.local"), PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(new string('a', 2001)), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.JustificativaInvalida);

    [Fact(DisplayName = "Perfil de governança só se pede no escopo da instituição")]
    public void Criar_GovernancaNaUnidade_Recusa() =>
        Codigos(PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "isabellapriscilafigueiredo@uniplus-test.local"), PerfilDeGovernanca(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.EscopoInvalido);

    [Fact(DisplayName = "Perfil sem versão em vigor não aceita pedido")]
    public void Criar_PerfilSemVersaoEmVigor_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Pendente("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], Catalogo);

        Codigos(PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro-viana84@uniplus-test.local"), perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.PerfilSemVersaoEmVigor);
    }

    [Fact(DisplayName = "Perfil de permissões institucionais só se pede no escopo da instituição")]
    public void Criar_PerfilInstitucionalNaUnidade_Recusa()
    {
        Perfil perfil = PerfisDeTeste.Vigente("Manutenção de motivos", "Mantém os motivos de isenção.", ["selecao:motivos:manter"], Catalogo);

        Codigos(PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro-viana84@uniplus-test.local"), perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.EscopoInvalido);
        PedidoDeConcessao.Criar(new PartesDoPedido(Betina, "leandro-viana84@uniplus-test.local"), perfil, EscopoDaConcessao.DaInstituicao(), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora)
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O aceite do beneficiário grava a identidade e o nome do token e libera o gestor")]
    public void Aceitar_PeloBeneficiario_GravaIdentidade()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Result aceite = pedido.Aceitar(Leandro, null, Agora.AddHours(1));

        aceite.IsSuccess.Should().BeTrue();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Beneficiario.Should().Be(Leandro.Identidade);
        pedido.BeneficiarioNome.Should().Be("Leandro Caleb Viana");
        pedido.BeneficiarioAceitouEm.Should().Be(Agora.AddHours(1));
    }

    [Theory(DisplayName = "Só o e-mail verificado do beneficiário aceita o pedido")]
    [InlineData("outra.pessoa@uniplus-test.local", true)]
    [InlineData("leandro-viana84@uniplus-test.local", false)]
    public void Aceitar_PorOutraPessoaOuSemVerificacao_Recusa(string email, bool verificado)
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Result aceite = pedido.Aceitar(Leandro with { Email = email, EmailVerificado = verificado }, null, Agora);

        Codigos(aceite).Should().Equal(PedidoErrorCodes.NaoEOBeneficiario);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedido.Beneficiario.Should().BeNull();
    }

    [Fact(DisplayName = "Aceite sem nome completo na sessão não libera o gestor")]
    public void Aceitar_SemNome_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Codigos(pedido.Aceitar(Leandro with { Nome = null }, null, Agora)).Should().Equal(PedidoErrorCodes.BeneficiarioSemNome);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
    }

    [Fact(DisplayName = "Pedido que não aguarda o aceite não é aceito de novo")]
    public void Aceitar_ForaDaSituacao_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Leandro, null);

        Codigos(pedido.Aceitar(Leandro, null, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    private static PedidoDeConcessao Criar(PessoaNoToken solicitante, string? email) => CriarResultado(solicitante, email).Value!;

    private static Result<PedidoDeConcessao> CriarResultado(PessoaNoToken solicitante, string? email) =>
        PedidoDeConcessao.Criar(
            new PartesDoPedido(solicitante, email), PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new DeclaracaoDoPedido(Justificativa), Dominios, Catalogo, Agora);
}
