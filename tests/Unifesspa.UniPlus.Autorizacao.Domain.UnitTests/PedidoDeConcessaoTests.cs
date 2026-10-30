namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PedidoDeConcessaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Justificativa = "A coordenação do CEPS precisa analisar os pedidos de atendimento.";

    private static readonly DateTimeOffset Agora = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly FakeCatalogo Catalogo = new();
    private static readonly FakeDominios Dominios = new();
    private static readonly ContextoDaAprovacao Contexto = new(BeneficiarioJaEEncarregada: false);

    private static readonly PessoaNoToken Betina = Pessoa("betina.neves", "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local");
    private static readonly PessoaNoToken Leandro = Pessoa("leandro.viana", "Leandro Caleb Viana", "leandro-viana84@uniplus-test.local");
    private static readonly PessoaNoToken Diogo = Pessoa("diogo.souza", "Diogo Bruno Filipe Souza", "diogo.bruno.souza@uniplus-test.local");
    private static readonly PessoaNoToken Isabella = Pessoa("isabella.figueiredo", "Isabella Priscila Figueiredo", "isabellapriscilafigueiredo@uniplus-test.local");
    private static readonly PessoaNoToken Marlene = Pessoa("marlene.martins", "Marlene Márcia Emily Martins", "marlenemarciamartins@uniplus-test.local");

    private static readonly PerfilAvaliado Interno = new(["selecao:processos:consultar"], "interno", false, null);
    private static readonly PerfilAvaliado Sensivel = new(["selecao:atendimento:analisar", "selecao:processos:consultar"], "sensivel", true, null);
    private static readonly PerfilAvaliado DeGestor = new(["autorizacao:perfis:manter"], "pessoal", true, TipoDeGovernanca.GestorDeAcessos);
    private static readonly PerfilAvaliado DeEncarregada = new(["autorizacao:concessoes:administrar"], "pessoal", true, TipoDeGovernanca.EncarregadaDeDados);

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
        perfil.Desativar();

        Result<PedidoDeConcessao> resultado = PedidoDeConcessao.Criar(
            Betina, "leandro@gmail.com", perfil, EscopoDaConcessao.DaInstituicao(), "curta", Dominios, Catalogo, Agora);

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
        Codigos(PedidoDeConcessao.Criar(
            Betina, "leandro-viana84@uniplus-test.local", PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), justificativa, Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.JustificativaInvalida);

    [Fact(DisplayName = "Justificativa com mais de 2000 caracteres é recusada")]
    public void Criar_JustificativaLonga_Recusa() =>
        Codigos(PedidoDeConcessao.Criar(
            Betina, "leandro-viana84@uniplus-test.local", PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), new string('a', 2001), Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.JustificativaInvalida);

    [Fact(DisplayName = "Perfil de governança só se pede no escopo da instituição")]
    public void Criar_GovernancaNaUnidade_Recusa() =>
        Codigos(PedidoDeConcessao.Criar(
            Betina, "isabellapriscilafigueiredo@uniplus-test.local", PerfilDeGovernanca(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Justificativa, Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.EscopoInvalido);

    [Fact(DisplayName = "Perfil de permissões institucionais só se pede no escopo da instituição")]
    public void Criar_PerfilInstitucionalNaUnidade_Recusa()
    {
        Perfil perfil = Perfil.Criar("Manutenção de motivos", "Mantém os motivos de isenção.", ["selecao:motivos:manter"], Catalogo).Value!;

        Codigos(PedidoDeConcessao.Criar(
            Betina, "leandro-viana84@uniplus-test.local", perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Justificativa, Dominios, Catalogo, Agora))
            .Should().Equal(PedidoErrorCodes.EscopoInvalido);
        PedidoDeConcessao.Criar(
            Betina, "leandro-viana84@uniplus-test.local", perfil, EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora)
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "O aceite do beneficiário grava a identidade e o nome do token e libera o gestor")]
    public void Aceitar_PeloBeneficiario_GravaIdentidade()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Result aceite = pedido.Aceitar(Leandro, Agora.AddHours(1));

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

        Result aceite = pedido.Aceitar(Leandro with { Email = email, EmailVerificado = verificado }, Agora);

        Codigos(aceite).Should().Equal(PedidoErrorCodes.NaoEOBeneficiario);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedido.Beneficiario.Should().BeNull();
    }

    [Fact(DisplayName = "Aceite sem nome completo na sessão não libera o gestor")]
    public void Aceitar_SemNome_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Codigos(pedido.Aceitar(Leandro with { Nome = null }, Agora)).Should().Equal(PedidoErrorCodes.BeneficiarioSemNome);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
    }

    [Fact(DisplayName = "Pedido que não aguarda o aceite não é aceito de novo")]
    public void Aceitar_ForaDaSituacao_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Leandro, null);

        Codigos(pedido.Aceitar(Leandro, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "Pedido de perfil só interno vale com a aprovação do gestor, que conclui e grava o retrato avaliado")]
    public void Aprovar_PerfilInterno_ConcluiComOGestor()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);
        pedido.Aceitar(Leandro, Agora);

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora.AddHours(1));

        resultado.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.EncerradoEm.Should().Be(Agora.AddHours(1));
        DecisaoDoPedido decisao = pedido.Decisoes.Single();
        decisao.Etapa.Should().Be(EtapaDoPedido.Gestor);
        decisao.Resultado.Should().Be(ResultadoDaDecisao.Aprovado);
        decisao.PermissoesAvaliadas.Should().Equal("selecao:processos:consultar");
        decisao.SensibilidadeAvaliada.Should().Be("interno");
        decisao.Dispensa.Should().BeNull();
    }

    [Fact(DisplayName = "Pedido de perfil sensível só vale depois da encarregada, sempre depois do gestor")]
    public void Aprovar_PerfilSensivel_PassaPelasDuasEtapas()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Result<DesfechoDaAprovacao> gestor = pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        gestor.Value.Should().Be(DesfechoDaAprovacao.AguardaEncarregada);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoEncarregada);
        pedido.EncerradoEm.Should().BeNull();
        pedido.EtapaAtual.Should().Be(EtapaDoPedido.Encarregada);

        Result<DesfechoDaAprovacao> encarregada = pedido.Aprovar(Marlene.Identidade, Sensivel, Contexto, Agora.AddHours(1));

        encarregada.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.Decisoes.Select(d => d.Etapa).Should().Equal(EtapaDoPedido.Gestor, EtapaDoPedido.Encarregada);
    }

    [Fact(DisplayName = "Pedido que não aguarda aprovador não se aprova nem se recusa")]
    public void Decidir_SemEtapa_Recusa()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        Codigos(pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        Codigos(pedido.Recusar(Diogo.Identidade, "Motivo.", Interno, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Theory(DisplayName = "Solicitante e beneficiário não decidem o pedido em que são parte")]
    [InlineData("solicitante")]
    [InlineData("beneficiario")]
    public void Decidir_ComoParte_Recusa(string parte)
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        Identidade decisor = parte == "solicitante" ? Betina.Identidade : Leandro.Identidade;

        Codigos(pedido.Aprovar(decisor, Interno, Contexto, Agora)).Should().Equal(PedidoErrorCodes.DecisorEParte);
        Codigos(pedido.Recusar(decisor, "Motivo.", Interno, Agora)).Should().Equal(PedidoErrorCodes.DecisorEParte);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.Decisoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Quem aprovou como gestor não decide como encarregada, nem aprovando nem recusando")]
    public void Decidir_MesmaPessoaNasDuasEtapas_Recusa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);

        Codigos(pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        Codigos(pedido.Recusar(Isabella.Identidade, "Motivo.", Sensivel, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoEncarregada);
    }

    [Fact(DisplayName = "Quem aprovou como gestor em qualquer ciclo não decide como encarregada, mesmo que outro gestor tenha aprovado depois")]
    public void Decidir_GestorDeCicloAnterior_NaoDecideComoEncarregada()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Isabella.Identidade, Sensivel, Contexto, Agora);
        PerfilAvaliado ampliado = new([.. Sensivel.Permissoes, "selecao:inscricoes:exportar"], "sensivel", true, null);
        Codigos(pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora)).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Aprovar(Diogo.Identidade, ampliado, Contexto, Agora.AddHours(2)).IsSuccess.Should().BeTrue();

        Codigos(pedido.Aprovar(Isabella.Identidade, ampliado, Contexto, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        Codigos(pedido.Recusar(Isabella.Identidade, "Motivo.", ampliado, Agora)).Should().Equal(PedidoErrorCodes.MesmoDecisorNasDuasEtapas);
        pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora.AddHours(3)).IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Perfil que mesmo com as mesmas permissões deixou de exigir a encarregada também volta ao gestor")]
    public void Aprovar_ExigenciaDaEncarregadaMudou_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        PerfilAvaliado menosSensivel = new(Sensivel.Permissoes, "interno", false, null);

        Codigos(pedido.Aprovar(Marlene.Identidade, menosSensivel, Contexto, Agora)).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Fact(DisplayName = "Pedido de perfil com designação nasce aguardando o documento, fora das etapas de decisão e do aceite")]
    public void Criar_PerfilComDesignacao_NasceAguardandoODocumento()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, Leandro.Email, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        pedido.Ativo.Should().BeTrue("o pedido repetido continua barrado enquanto espera o documento");
        pedido.EtapaAtual.Should().BeNull();
        Codigos(pedido.Aceitar(Leandro, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        Codigos(pedido.Aprovar(Diogo.Identidade, DeGestor, Contexto, Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        pedido.NovoAnexo(1024).IsSuccess.Should().BeTrue("o solicitante anexa o documento justamente nesta situação");
    }

    [Fact(DisplayName = "Pedido de perfil com designação pode ser cancelado enquanto espera o documento")]
    public void Cancelar_AguardandoDocumento_Encerra()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, Leandro.Email, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;

        pedido.Cancelar(Agora).IsSuccess.Should().BeTrue();

        pedido.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
    }

    [Theory(DisplayName = "O documento verificado libera o pedido para o aceite do beneficiário, ou para o gestor no pedido para si mesmo")]
    [InlineData(false, SituacaoDoPedido.AguardandoBeneficiario)]
    [InlineData(true, SituacaoDoPedido.AguardandoGestor)]
    public void LiberarAoVerificarDocumento_SegueParaAEtapaSeguinte(bool paraSiMesmo, SituacaoDoPedido esperada)
    {
        PessoaNoToken solicitante = paraSiMesmo ? Leandro : Betina;
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            solicitante, Leandro.Email, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;

        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o anexo ainda não foi confirmado");
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora).IsSuccess.Should().BeTrue();
        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o anexo está em verificação, sem veredicto");
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);

        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeTrue();
        pedido.LiberarAoVerificarDocumento().Should().BeTrue();

        pedido.Situacao.Should().Be(esperada);
        pedido.LiberarAoVerificarDocumento().Should().BeFalse("o pedido já seguiu");
    }

    [Fact(DisplayName = "O documento recusado pelo verificador não libera o pedido")]
    public void LiberarAoVerificarDocumento_AnexoRecusado_NaoLibera()
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, Leandro.Email, PerfilDeGovernanca(), EscopoDaConcessao.DaInstituicao(), Justificativa, Dominios, Catalogo, Agora).Value!;
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);
        anexo.RegistrarRecusado(Agora).Should().BeTrue();

        pedido.LiberarAoVerificarDocumento().Should().BeFalse();

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
    }

    [Fact(DisplayName = "O pedido da encarregada exige o documento de designação e passa só pelo gestor, com a dispensa na trilha")]
    public void Aprovar_PerfilDaEncarregada_DispensaASegundaAprovacao()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor(PerfilDeGovernanca(TipoDeGovernanca.EncarregadaDeDados));
        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, DeEncarregada, Contexto, Agora);

        resultado.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.Aprovado);
        pedido.Decisoes.Single().Dispensa.Should().Be(MotivosDeDispensa.DesignacaoDaEncarregada);
    }

    [Fact(DisplayName = "Beneficiário que já é a encarregada tem o pedido sensível concluído pelo gestor, com a dispensa na trilha")]
    public void Aprovar_BeneficiarioJaEEncarregada_DispensaASegundaAprovacao()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Diogo.Identidade, Sensivel, new ContextoDaAprovacao(true), Agora);

        resultado.Value.Should().Be(DesfechoDaAprovacao.Concluido);
        pedido.Decisoes.Single().Dispensa.Should().Be(MotivosDeDispensa.BeneficiariaJaEEncarregada);
    }

    [Fact(DisplayName = "A dispensa por já ser encarregada não vale quando o perfil não exige a encarregada")]
    public void Aprovar_PerfilInterno_NaoRegistraDispensa()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        pedido.Aprovar(Diogo.Identidade, Interno, new ContextoDaAprovacao(true), Agora);

        pedido.Decisoes.Single().Dispensa.Should().BeNull();
    }

    [Fact(DisplayName = "Perfil que mudou entre as etapas devolve o pedido ao gestor e nada é concedido")]
    public void Aprovar_PerfilAlterado_VoltaAoGestor()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        PerfilAvaliado ampliado = new([.. Sensivel.Permissoes, "selecao:inscricoes:exportar"], "sensivel", true, null);

        Result<DesfechoDaAprovacao> resultado = pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora.AddHours(1));

        Codigos(resultado).Should().Equal(PedidoErrorCodes.PerfilAlterado);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        pedido.EncerradoEm.Should().BeNull();
        pedido.Decisoes.Select(d => d.Resultado).Should().Equal(ResultadoDaDecisao.Aprovado, ResultadoDaDecisao.PerfilAlterado);
        DecisaoDoPedido tentativa = pedido.Decisoes[1];
        tentativa.Etapa.Should().Be(EtapaDoPedido.Encarregada);
        tentativa.DecisorSub.Should().Be(Marlene.Identidade.Sub);
        tentativa.PermissoesAvaliadas.Should().Equal(ampliado.Permissoes);

        // O gestor decide de novo, agora sobre o perfil ampliado, e a encarregada aprova o que ele avaliou.
        pedido.Aprovar(Isabella.Identidade, ampliado, Contexto, Agora.AddHours(2)).Value.Should().Be(DesfechoDaAprovacao.AguardaEncarregada);
        pedido.Aprovar(Marlene.Identidade, ampliado, Contexto, Agora.AddHours(3)).Value.Should().Be(DesfechoDaAprovacao.Concluido);
    }

    [Theory(DisplayName = "Perfil desativado não aceita aprovação em nenhuma etapa")]
    [InlineData(false)]
    [InlineData(true)]
    public void Aprovar_PerfilInativo_Recusa(bool naEtapaDaEncarregada)
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        if (naEtapaDaEncarregada)
        {
            pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        }

        PerfilAvaliado inativo = new(Sensivel.Permissoes, Sensivel.Sensibilidade, true, null, ativo: false);
        SituacaoDoPedido antes = pedido.Situacao;

        Codigos(pedido.Aprovar(naEtapaDaEncarregada ? Marlene.Identidade : Diogo.Identidade, inativo, Contexto, Agora))
            .Should().Equal(PedidoErrorCodes.PerfilInativo);
        pedido.Situacao.Should().Be(antes);
        pedido.Decisoes.Count.Should().Be(naEtapaDaEncarregada ? 1 : 0);
    }

    [Fact(DisplayName = "Recusa encerra o pedido com o motivo e o retrato avaliado, sem concessão")]
    public void Recusar_EncerraComMotivo()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Result recusa = pedido.Recusar(Diogo.Identidade, "  Função não exige acesso a laudos.  ", Sensivel, Agora.AddHours(1));

        recusa.IsSuccess.Should().BeTrue();
        pedido.Situacao.Should().Be(SituacaoDoPedido.Recusado);
        pedido.EncerradoEm.Should().Be(Agora.AddHours(1));
        pedido.ConcessaoId.Should().BeNull();
        DecisaoDoPedido decisao = pedido.Decisoes.Single();
        decisao.Resultado.Should().Be(ResultadoDaDecisao.Recusado);
        decisao.Motivo.Should().Be("Função não exige acesso a laudos.");
        decisao.PermissoesAvaliadas.Should().Equal(Sensivel.Permissoes);
    }

    [Theory(DisplayName = "Recusa sem motivo, com motivo longo ou com caractere nulo é recusada e não encerra")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("Mot\0ivo")]
    public void Recusar_MotivoInvalido_Recusa(string? motivo)
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();

        Codigos(pedido.Recusar(Diogo.Identidade, motivo, Interno, Agora)).Should().Equal(PedidoErrorCodes.MotivoInvalido);
        Codigos(pedido.Recusar(Diogo.Identidade, new string('a', 501), Interno, Agora)).Should().Equal(PedidoErrorCodes.MotivoInvalido);
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
    }

    [Fact(DisplayName = "O solicitante cancela antes da primeira decisão; depois dela, não")]
    public void Cancelar_SoAntesDaPrimeiraDecisao()
    {
        PedidoDeConcessao cancelavel = PedidoAguardandoGestor();
        cancelavel.Cancelar(Agora.AddHours(1)).IsSuccess.Should().BeTrue();
        cancelavel.Situacao.Should().Be(SituacaoDoPedido.Cancelado);
        cancelavel.EncerradoEm.Should().Be(Agora.AddHours(1));
        cancelavel.Ativo.Should().BeFalse();

        PedidoDeConcessao decidido = PedidoAguardandoGestor();
        decidido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        Codigos(decidido.Cancelar(Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);

        PedidoDeConcessao devolvido = PedidoAguardandoGestor();
        devolvido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);
        devolvido.Aprovar(Marlene.Identidade, new PerfilAvaliado(["outra"], "sensivel", true, null), Contexto, Agora);
        devolvido.Situacao.Should().Be(SituacaoDoPedido.AguardandoGestor);
        Codigos(devolvido.Cancelar(Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
    }

    [Fact(DisplayName = "O anexo só se envia e se confirma antes da primeira decisão")]
    public void Anexo_SoAntesDaPrimeiraDecisao()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        AnexoDoPedido anexo = pedido.NovoAnexo(2048).Value!;
        anexo.ChaveDeEnvio.Should().Be($"autorizacao/pedido/{pedido.Id}/anexo/{anexo.Id}/envio");
        anexo.ChaveDefinitiva.Should().Be($"autorizacao/pedido/{pedido.Id}/anexo/{anexo.Id}/definitivo");

        pedido.Aprovar(Diogo.Identidade, Sensivel, Contexto, Agora);

        Codigos(pedido.NovoAnexo(2048)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        Codigos(pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);
        anexo.Situacao.Should().Be(SituacaoDoAnexo.AguardandoEnvio);
    }

    [Theory(DisplayName = "O PDF do anexo tem de 1 byte a 10 MB")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10L * 1024 * 1024 + 1)]
    public void Anexo_TamanhoInvalido_Recusa(long tamanho) =>
        Codigos(PedidoAguardandoGestor().NovoAnexo(tamanho)).Should().Equal(PedidoErrorCodes.AnexoInvalido);

    [Fact(DisplayName = "Confirmar o anexo exige o ETag e leva o anexo à verificação; o resultado repetido não muda nada")]
    public void Anexo_Confirmar_EVeredicto()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        AnexoDoPedido anexo = pedido.NovoAnexo(2048).Value!;

        Codigos(pedido.ConfirmarAnexo(anexo.Id, "  ", Agora)).Should().Equal(PedidoErrorCodes.AnexoInvalido);
        Codigos(pedido.ConfirmarAnexo(Guid.NewGuid(), "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.NaoEncontrado);
        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeFalse("o anexo ainda não está em verificação");

        pedido.ConfirmarAnexo(anexo.Id, " \"etag\" ", Agora).IsSuccess.Should().BeTrue();
        anexo.Situacao.Should().Be(SituacaoDoAnexo.EmVerificacao);
        anexo.ETag.Should().Be("\"etag\"");
        Codigos(pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora)).Should().Equal(PedidoErrorCodes.SituacaoInvalida);

        anexo.RegistrarRecusado(Agora).Should().BeTrue();
        anexo.RegistrarVerificado(new string('a', 64), Agora).Should().BeFalse("o veredicto repetido não reabre o anexo");
        anexo.Situacao.Should().Be(SituacaoDoAnexo.Recusado);
        pedido.TemAnexoVerificado.Should().BeFalse();
    }

    [Fact(DisplayName = "As partes do pedido são o solicitante e o beneficiário, este também pelo e-mail antes do aceite")]
    public void EhParte_SolicitanteEBeneficiario()
    {
        PedidoDeConcessao pedido = Criar(Betina, Leandro.Email);

        pedido.EhParte(Betina).Should().BeTrue();
        pedido.EhParte(Leandro).Should().BeTrue("o e-mail verificado dele é o do pedido");
        pedido.EhParte(Leandro with { EmailVerificado = false }).Should().BeFalse();
        pedido.EhParte(Diogo).Should().BeFalse();

        pedido.Aceitar(Leandro, Agora);
        pedido.EhParte(Leandro with { Email = "outro@uniplus-test.local" }).Should().BeTrue("já aceitou: vale a identidade");
    }

    [Fact(DisplayName = "Só o pedido aprovado registra a concessão")]
    public void RegistrarConcessao_SoNoPedidoAprovado()
    {
        PedidoDeConcessao pedido = PedidoAguardandoGestor();
        Guid concessao = Guid.NewGuid();

        FluentActions.Invoking(() => pedido.RegistrarConcessao(concessao)).Should().Throw<InvalidOperationException>();

        pedido.Aprovar(Diogo.Identidade, Interno, Contexto, Agora);
        pedido.RegistrarConcessao(concessao);
        pedido.ConcessaoId.Should().Be(concessao);
    }

    private static PedidoDeConcessao PedidoAguardandoGestor(Perfil? perfil = null)
    {
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            Betina, Leandro.Email, perfil ?? PerfilComum(), EscopoOf(perfil), Justificativa, Dominios, Catalogo, Agora).Value!;
        if (pedido.Situacao == SituacaoDoPedido.AguardandoDocumento)
        {
            AnexoVerificado(pedido);
            pedido.LiberarAoVerificarDocumento().Should().BeTrue();
        }

        pedido.Aceitar(Leandro, Agora).IsSuccess.Should().BeTrue();
        return pedido;
    }

    private static void AnexoVerificado(PedidoDeConcessao pedido)
    {
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", Agora);
        anexo.RegistrarVerificado(new string('a', 64), Agora);
    }

    private static PedidoDeConcessao Criar(PessoaNoToken solicitante, string? email) =>
        CriarResultado(solicitante, email).Value!;

    private static Result<PedidoDeConcessao> CriarResultado(PessoaNoToken solicitante, string? email) =>
        PedidoDeConcessao.Criar(
            solicitante, email, PerfilComum(), EscopoDaConcessao.DaUnidade(Guid.NewGuid()), Justificativa, Dominios, Catalogo, Agora);

    private static EscopoDaConcessao EscopoOf(Perfil? perfil) =>
        perfil?.Governanca is null ? EscopoDaConcessao.DaUnidade(Guid.NewGuid()) : EscopoDaConcessao.DaInstituicao();

    private static PessoaNoToken Pessoa(string sub, string nome, string email) =>
        new(new Identidade(Emissor, sub), nome, email, true);

    private static Perfil PerfilComum() =>
        Perfil.Criar("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], Catalogo).Value!;

    private static Perfil PerfilDeGovernanca(TipoDeGovernanca governanca = TipoDeGovernanca.GestorDeAcessos)
    {
        Perfil perfil = Perfil.Criar("Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], Catalogo).Value!;
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!.SetValue(perfil, governanca);
        return perfil;
    }

    private static string[] Codigos(Result resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private static string[] Codigos<T>(Result<T> resultado) => [.. resultado.Errors.Select(e => e.Error.Code)];

    private sealed class FakeCatalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:processos:", StringComparison.Ordinal);
    }

    private sealed class FakeDominios : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => string.Equals(dominio, "uniplus-test.local", StringComparison.OrdinalIgnoreCase);
    }
}
