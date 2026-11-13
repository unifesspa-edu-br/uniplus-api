namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

/// <summary>A verificação de designação decide pela porta, sem guardar resposta, e entra na ordem do motor.</summary>
public sealed class DesignacaoNaBancaCheckTests
{
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly Guid Fase = Guid.CreateVersion7();
    private static readonly Guid Etapa = Guid.CreateVersion7();
    private static readonly Guid Banca = Guid.CreateVersion7();
    private static readonly VinculosDaBanca Vinculos = VinculosDaBanca.From(Fase, Banca, Etapa).Value!;

    [Fact(DisplayName = "Aprova a pessoa designada à banca do resource")]
    public async Task Designada_Aprova()
    {
        CheckResult resultado = await Verificar(new DesignacoesEmMemoria().Designar(Processo, Vinculos), ResourceDaBanca());

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Recusa por fora_da_designacao a pessoa designada a outra banca")]
    public async Task DesignadaAOutraBanca_RecusaForaDaDesignacao()
    {
        CheckResult resultado = await Verificar(new DesignacoesEmMemoria().Designar(Processo, VinculosDaBanca.From(Fase, Guid.CreateVersion7(), Etapa).Value!), ResourceDaBanca());

        resultado.Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
        ValoresCanonicos.De(resultado.Motivo!.Value).Should().Be("fora_da_designacao");
    }

    [Fact(DisplayName = "Recusa por contexto_obrigatorio_ausente o resource sem os vínculos da banca, sem consultar a porta")]
    public async Task SemVinculos_RecusaContextoAusente()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);

        CheckResult resultado = await Verificar(porta, Resource(processoId: Processo));

        resultado.Motivo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
        porta.Consultas.Should().Be(0);
    }

    [Fact(DisplayName = "Recusa por contexto_obrigatorio_ausente o resource da banca sem processo")]
    public async Task SemProcesso_RecusaContextoAusente()
    {
        ResourceContext resource = Resource().ComVinculosDaBanca(VinculosDaBanca.From(Fase, Banca).Value!);

        CheckResult resultado = await Verificar(new DesignacoesEmMemoria().Designar(Processo, Vinculos), resource);

        resultado.Motivo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
    }

    [Fact(DisplayName = "A designação não é guardada: a revogação e a nova designação valem na chamada seguinte")]
    public async Task RespostaDaPortaMuda_CadaChamadaConsultaDeNovo()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);
        DesignacaoNaBancaCheck check = new(porta);
        ResourceContext resource = ResourceDaBanca();

        CheckResult antes = await Chamar(check, resource);
        porta.Revogar(Processo, Vinculos);
        CheckResult depois = await Chamar(check, resource);
        porta.Designar(Processo, Vinculos);
        CheckResult denovo = await Chamar(check, resource);

        antes.Passou.Should().BeTrue();
        depois.Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
        denovo.Passou.Should().BeTrue();
        porta.Consultas.Should().Be(3);
    }

    [Fact(DisplayName = "A designação à banca não vale em outra etapa, outra fase nem outro processo")]
    public async Task DesignacaoNoutroLugar_RecusaForaDaDesignacao()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);

        (await Verificar(porta, ResourceDaBanca(etapa: Guid.CreateVersion7()))).Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
        (await Verificar(porta, ResourceDaBanca(fase: Guid.CreateVersion7()))).Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
        (await Verificar(porta, ResourceDaBanca(processo: Guid.CreateVersion7()))).Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
    }

    [Fact(DisplayName = "Escopo de chamada e de unidade do resource não substituem a designação")]
    public async Task EscopoDoResource_NaoSubstituiADesignacao()
    {
        ResourceContext resource = Resource(unidadeProprietariaId: Guid.CreateVersion7(), processoId: Processo, chamadaId: Guid.CreateVersion7())
            .ComVinculosDaBanca(VinculosDaBanca.From(Fase, Banca, Etapa).Value!);

        CheckResult resultado = await Verificar(new DesignacoesEmMemoria(), resource);

        resultado.Motivo.Should().Be(MotivoNegativa.ForaDaDesignacao);
    }

    [Fact(DisplayName = "O motor recusa por sem_concessao_aplicavel antes de consultar a designação")]
    public async Task Motor_SemConcessao_NaoConsultaADesignacao()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);

        AuthorizationDecision decisao = await Decidir(porta, Sujeito(), ResourceDaBanca());

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
        porta.Consultas.Should().Be(0);
    }

    [Fact(DisplayName = "O motor recusa por impedimento de inscrição antes de consultar a designação")]
    public async Task Motor_Inscrito_NaoConsultaADesignacao()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);

        AuthorizationDecision decisao = await Decidir(porta, Sujeito(concessoes: Concessao()), ResourceDaBanca(), new InscricoesEmMemoria(Processo));

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ImpedidoPorInscricaoNoProcesso);
        porta.Consultas.Should().Be(0);
    }

    [Fact(DisplayName = "O motor concede a quem tem a concessão e a designação, e recusa quem só tem a concessão")]
    public async Task Motor_ConcessaoEDesignacao()
    {
        AuthorizationSubject sujeito = Sujeito(concessoes: Concessao());

        (await Decidir(new DesignacoesEmMemoria().Designar(Processo, Vinculos), sujeito, ResourceDaBanca())).Allowed.Should().BeTrue();
        (await Decidir(new DesignacoesEmMemoria(), sujeito, ResourceDaBanca())).DenyReason!.Codigo
            .Should().Be(MotivoNegativa.ForaDaDesignacao);
    }

    [Fact(DisplayName = "A avaliação sem etapa ou sem inscrição é recusada por contexto_obrigatorio_ausente antes da designação")]
    public async Task Motor_ContextoDaAvaliacaoIncompleto_NaoConsultaADesignacao()
    {
        DesignacoesEmMemoria porta = new DesignacoesEmMemoria().Designar(Processo, Vinculos);
        PermissionRequirement requisito = Requisito(
            escopoContextoObrigatorio: [CamposDeContexto.EtapaId, CamposDeContexto.InscricaoId, CamposDeContexto.BancaId],
            verificacoesDeContexto: [DesignacaoNaBancaCheck.NomeCanonico]);
        ResourceContext semEtapa = Resource(processoId: Processo, resourceId: null)
            .ComVinculosDaBanca(VinculosDaBanca.From(Fase, Banca).Value!);

        AuthorizationDecision decisao = await Decidir(porta, Sujeito(concessoes: Concessao()), semEtapa, requisito: requisito);

        decisao.DenyReason!.Codigo.Should().Be(MotivoNegativa.ContextoObrigatorioAusente);
        porta.Consultas.Should().Be(0);
    }

    private static ResourceContext ResourceDaBanca(Guid? processo = null, Guid? fase = null, Guid? etapa = null) =>
        Resource(processoId: processo ?? Processo).ComVinculosDaBanca(VinculosDaBanca.From(fase ?? Fase, Banca, etapa ?? Etapa).Value!);

    private static Task<CheckResult> Verificar(IDesignacaoDeBanca porta, ResourceContext resource) =>
        Chamar(new DesignacaoNaBancaCheck(porta), resource);

    private static Task<CheckResult> Chamar(DesignacaoNaBancaCheck check, ResourceContext resource) =>
        check.CheckAsync(Sujeito(), Requisito(), resource, Requisicao(), CancellationToken.None);

    private static Task<AuthorizationDecision> Decidir(
        IDesignacaoDeBanca porta,
        AuthorizationSubject sujeito,
        ResourceContext resource,
        IInscricaoDoSujeitoNoProcesso? inscricoes = null,
        PermissionRequirement? requisito = null) =>
        new AuthorizationDecisionService(
            new GrantEfetivoAplicavelCheck(),
            new ImpedimentoPorInscricaoCheck(inscricoes ?? new InscricoesEmMemoria()),
            [new DesignacaoNaBancaCheck(porta)])
        .DecidirAsync(
            sujeito,
            requisito ?? Requisito(verificacoesDeContexto: [DesignacaoNaBancaCheck.NomeCanonico]),
            resource,
            Requisicao(),
            CancellationToken.None);
}
