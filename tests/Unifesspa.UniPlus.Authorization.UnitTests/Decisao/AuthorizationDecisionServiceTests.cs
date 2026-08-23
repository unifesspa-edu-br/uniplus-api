namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

/// <summary>
/// Cenários de aceite da Task #1244 sobre o ponto de decisão único: concede pela
/// concessão do token, nega com o motivo correto em cada situação e registra
/// toda decisão.
/// </summary>
public sealed class AuthorizationDecisionServiceTests
{
    private readonly RegistroEmMemoria _registro = new();

    [Fact(DisplayName = "Concede quando a permissão está entre as concessões do token, registrando a fonte")]
    public async Task Decide_ComConcessaoDoToken_Concede()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito());

        decisao.Allowed.Should().BeTrue();
        decisao.DenyReason.Should().BeNull();
        decisao.GrantUsed!.Fonte.Should().Be(FonteGrant.Token,
            "a auditoria precisa saber de qual fonte veio a concessão que autorizou");
    }

    [Fact(DisplayName = "Nega por sem_concessao_aplicavel quando a permissão não está entre as concessões")]
    public async Task Decide_SemConcessao_NegaPorSemConcessaoAplicavel()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao("configuracao:outra-coisa:manter")),
            Requisito());

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
        decisao.GrantUsed.Should().BeNull();
    }

    [Fact(DisplayName = "Nega quando o sujeito não tem concessão alguma")]
    public async Task Decide_SujeitoSemConcessoes_Nega()
    {
        AuthorizationDecision decisao = await Decidir(Sujeito(), Requisito());

        decisao.Allowed.Should().BeFalse();
        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Nega por concessao_expirada quando a única concessão da permissão venceu")]
    public async Task Decide_ConcessaoVencida_NegaPorConcessaoExpirada()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao(
                fonte: FonteGrant.OidcGroupBinding,
                validoAte: Agora.AddSeconds(-1))),
            Requisito());

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ConcessaoExpirada,
            "distinguir 'venceu' de 'nunca existiu' é o que diz se a correção é renovar ou conceder");
    }

    [Fact(DisplayName = "Nega por contexto_obrigatorio_ausente quando o recurso não traz o escopo exigido")]
    public async Task Decide_EscopoExigidoAusente_NegaPorContextoObrigatorio()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(escopoContextoObrigatorio: ["processoId"]),
            Recurso(processoId: null));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
    }

    [Fact(DisplayName = "Concede quando o escopo exigido está presente no recurso")]
    public async Task Decide_EscopoExigidoPresente_Concede()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(escopoContextoObrigatorio: ["processoId"]),
            Recurso(processoId: Guid.CreateVersion7()));

        decisao.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Nega quando a permissão exige um campo de contexto que o backend não conhece")]
    public async Task Decide_CampoDeContextoDesconhecido_Nega()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(escopoContextoObrigatorio: ["bancaId"]));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente,
            "um contexto que não se sabe verificar não pode ser dado por satisfeito");
    }

    [Theory(DisplayName = "Nega por multifator_nao_satisfeito toda permissão que exija multifator")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decide_PermissaoExigeMfa_Nega(bool mfaSatisfeito)
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(mfaSatisfeito, Concessao()),
            Requisito(requerMfa: true));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.MultifatorNaoSatisfeito,
            "MfaSatisfeito é informado por quem monta o sujeito — tomá-lo como prova faria um "
            + "booleano do chamador valer por uma verificação que ninguém executou");
    }

    [Fact(DisplayName = "Nega por dupla_aprovacao_invalida qualquer permissão que exija dupla aprovação")]
    public async Task Decide_PermissaoExigeDuplaAprovacao_Nega()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(mfaSatisfeito: true, concessoes: Concessao()),
            Requisito(requerDuplaAprovacao: true));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.DuplaAprovacaoInvalida,
            "a verificação de dupla aprovação não existe nesta fatia — conceder seria falha aberta");
    }

    [Theory(DisplayName = "Nega por base_legal_ausente quando o dado é pessoal ou sensível sem base legal")]
    [InlineData(Sensibilidade.Pessoal)]
    [InlineData(Sensibilidade.Sensivel)]
    public async Task Decide_DadoPessoalSemBaseLegal_Nega(Sensibilidade sensibilidade)
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(sensibilidade, baseLegalPadrao: null),
            Recurso(sensibilidade));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.BaseLegalAusente);
    }

    [Fact(DisplayName = "Nega quando a permissão é sensível, ainda que o contexto se declare interno")]
    public async Task Decide_PermissaoSensivelComRecursoInterno_Nega()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(Sensibilidade.Sensivel, baseLegalPadrao: null),
            Recurso(Sensibilidade.Interna));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.BaseLegalAusente,
            "a sensibilidade é declarada em dois lugares e vale a maior — olhar só o contexto "
            + "deixaria a operação classificada como sensível sair sem base legal");
    }

    [Fact(DisplayName = "Nega quando o recurso é pessoal, ainda que a permissão se declare interna")]
    public async Task Decide_RecursoPessoalComPermissaoInterna_Nega()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(Sensibilidade.Interna, baseLegalPadrao: null),
            Recurso(Sensibilidade.Pessoal));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.BaseLegalAusente);
    }

    [Fact(DisplayName = "Concede dado pessoal quando a permissão declara base legal")]
    public async Task Decide_DadoPessoalComBaseLegal_Concede()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(Sensibilidade.Pessoal, baseLegalPadrao: "LGPD art. 7º, II"),
            Recurso(Sensibilidade.Pessoal));

        decisao.Allowed.Should().BeTrue();
    }

    [Fact(DisplayName = "Ordem canônica: a falta de contexto precede o multifator")]
    public async Task Decide_ContextoAusenteEMfaAusente_NegaPeloContexto()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(mfaSatisfeito: false, concessoes: Concessao()),
            Requisito(requerMfa: true, escopoContextoObrigatorio: ["processoId"]));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente,
            "a pré-condição estrutural é avaliada antes da cadeia (ADR-0078)");
    }

    [Fact(DisplayName = "Ordem canônica: o multifator precede a seleção de concessão")]
    public async Task Decide_MfaAusenteESemConcessao_NegaPeloMultifator()
    {
        AuthorizationDecision decisao = await Decidir(
            Sujeito(mfaSatisfeito: false),
            Requisito(requerMfa: true));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.MultifatorNaoSatisfeito);
    }

    [Fact(DisplayName = "Ordem canônica: a seleção de concessão precede as verificações declaradas")]
    public async Task Decide_SemConcessaoEComCheckQueReprova_NegaPelaConcessao()
    {
        CheckDuplo check = new("fase-aberta", CheckResult.Reprovado(MotivoNegativa.FaseFechada));

        AuthorizationDecision decisao = await Decidir(
            Sujeito(),
            Requisito(verificacoesDeContexto: ["fase-aberta"]),
            checks: [check]);

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
        check.Execucoes.Should().Be(0,
            "sem concessão aplicável a decisão já está tomada — não se gasta verificação de contexto");
    }

    [Fact(DisplayName = "Ordem canônica: as verificações declaradas precedem a base legal")]
    public async Task Decide_CheckReprovaEBaseLegalAusente_NegaPeloCheck()
    {
        CheckDuplo check = new("fase-aberta", CheckResult.Reprovado(MotivoNegativa.FaseFechada));

        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(Sensibilidade.Pessoal, verificacoesDeContexto: ["fase-aberta"]),
            Recurso(Sensibilidade.Pessoal),
            checks: [check]);

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.FaseFechada);
    }

    [Fact(DisplayName = "Executa a verificação declarada e concede quando ela aprova")]
    public async Task Decide_CheckDeclaradoAprova_Concede()
    {
        CheckDuplo check = new("fase-aberta", CheckResult.Aprovado());

        AuthorizationDecision decisao = await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(verificacoesDeContexto: ["fase-aberta"]),
            checks: [check]);

        decisao.Allowed.Should().BeTrue();
        check.Execucoes.Should().Be(1);
    }

    [Fact(DisplayName = "Verificação declarada e não registrada é erro de composição, não negativa")]
    public async Task Decide_CheckDeclaradoNaoRegistrado_Lanca()
    {
        Func<Task> acao = () => Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(verificacoesDeContexto: ["verificacao-inexistente"]));

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "traduzir defeito de composição em 403 esconderia o defeito atrás de uma negativa plausível");
    }

    [Fact(DisplayName = "Registra a decisão permitida com ator, permissão, resultado e fonte da concessão")]
    public async Task Decide_Permitida_RegistraDecisao()
    {
        await Decidir(Sujeito(concessoes: Concessao(grantId: Guid.CreateVersion7())), Requisito());

        RegistroDecisaoAcesso registro = _registro.Registrados.Should().ContainSingle().Subject;
        registro.Permitido.Should().BeTrue();
        registro.Permissao.Should().Be(Permissao);
        registro.RequestId.Should().Be("req-1244");
        registro.Instante.Should().Be(Agora);
        registro.FonteGrant.Should().Be("token");
        registro.MotivoNegativa.Should().BeNull();
    }

    [Fact(DisplayName = "Registra também a decisão negada, com o motivo em valor canônico")]
    public async Task Decide_Negada_RegistraMotivoCanonico()
    {
        await Decidir(Sujeito(), Requisito());

        RegistroDecisaoAcesso registro = _registro.Registrados.Should().ContainSingle().Subject;
        registro.Permitido.Should().BeFalse();
        registro.MotivoNegativa.Should().Be("sem_concessao_aplicavel",
            "o registro é lido fora do processo — o contrato do vocabulário é o valor canônico");
        registro.FonteGrant.Should().BeNull();
    }

    [Fact(DisplayName = "O registro não carrega identificador de token")]
    public async Task Decide_Registro_NaoCarregaIdentificadorDeToken()
    {
        await Decidir(Sujeito(concessoes: Concessao()), Requisito());

        typeof(RegistroDecisaoAcesso).GetProperties().Select(static p => p.Name)
            .Should().NotContain(["Subject", "Jti", "Emissor"],
                "subject e jti identificam pessoa e sessão — log não é lugar deles");
    }

    [Fact(DisplayName = "O registro grava a sensibilidade efetiva, não a menor das duas declarações")]
    public async Task Decide_SensibilidadesDivergentes_RegistraAMaisRestritiva()
    {
        await Decidir(
            Sujeito(concessoes: Concessao()),
            Requisito(Sensibilidade.Sensivel, baseLegalPadrao: "LGPD art. 11, II"),
            Recurso(Sensibilidade.Interna));

        _registro.Registrados.Should().ContainSingle().Which.Sensibilidade.Should().Be(
            "sensivel",
            "registrar a classificação menor faria a operação sensível constar como interna, e "
            + "consultas de conformidade sobre o registro deixariam de encontrá-la");
    }

    private Task<AuthorizationDecision> Decidir(
        AuthorizationSubject sujeito,
        PermissionRequirement requisito,
        ResourceContext? recurso = null,
        IEnumerable<IPermissaoCheck>? checks = null)
    {
        AuthorizationDecisionService servico = new(
            new GrantEfetivoAplicavelCheck(),
            checks ?? [],
            _registro);

        return servico.DecideAsync(
            sujeito,
            requisito,
            recurso ?? Recurso(),
            Requisicao(),
            CancellationToken.None);
    }
}
