namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

using static Unifesspa.UniPlus.Authorization.UnitTests.Decisao.CenariosDeDecisao;

/// <summary>
/// Seleção da concessão aplicável: alcance de escopo, validade e desempate.
/// </summary>
public sealed class GrantEfetivoAplicavelCheckTests
{
    private static readonly Guid Unidade = Guid.CreateVersion7();
    private static readonly Guid OutraUnidade = Guid.CreateVersion7();
    private static readonly Guid Processo = Guid.CreateVersion7();

    private readonly GrantEfetivoAplicavelCheck _check = new();

    [Fact(DisplayName = "Concessão institucional alcança recurso sem unidade proprietária")]
    public async Task Check_InstitucionalEmRecursoSemUnidade_Alcanca()
    {
        CheckResult resultado = await Verificar(Concessao(), Recurso(processoId: Processo));

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão institucional não alcança recurso de uma unidade, sem herança")]
    public async Task Check_InstitucionalEmRecursoDeUnidade_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(Concessao(), Recurso(unidadeProprietariaId: Unidade));

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão de unidade não alcança recurso institucional")]
    public async Task Check_UnidadeEmRecursoSemUnidade_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(Concessao(escopoUnidadeId: Unidade), Recurso());

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão expirada na unidade A não encobre a vigente na unidade B")]
    public async Task Check_ExpiradaEmOutraUnidade_NaoEncobreAVigente()
    {
        EffectiveGrant vigente = Concessao(grantId: Guid.CreateVersion7(), escopoUnidadeId: OutraUnidade);

        CheckResult resultado = await Verificar(
            [Concessao(escopoUnidadeId: Unidade, validoAte: Agora.AddDays(-1)), vigente],
            Recurso(unidadeProprietariaId: OutraUnidade));

        resultado.GrantSelecionado.Should().Be(vigente);
    }

    [Fact(DisplayName = "Concessão vencida na unidade do recurso nega por concessao_expirada")]
    public async Task Check_VencidaNaUnidadeDoRecurso_NegaPorExpiracao()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade, validoAte: Agora.AddDays(-1)),
            Recurso(unidadeProprietariaId: Unidade));

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão por unidade alcança recurso da mesma unidade")]
    public async Task Check_EscopoDeUnidadeIgual_Alcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade),
            Recurso(unidadeProprietariaId: Unidade));

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão por unidade não alcança recurso de outra unidade")]
    public async Task Check_EscopoDeUnidadeDiferente_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade),
            Recurso(unidadeProprietariaId: OutraUnidade));

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão com escopo não alcança recurso sem aquele escopo")]
    public async Task Check_RecursoSemEscopoQueAConcessaoRestringe_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoProcessoId: Processo),
            Recurso(processoId: null));

        resultado.Passou.Should().BeFalse(
            "a ausência do escopo no recurso não relaxa a restrição da concessão");
    }

    [Fact(DisplayName = "Restrição por tipo de recurso alcança apenas o tipo restrito")]
    public async Task Check_RestricaoDeTipo_AlcancaSomenteOTipo()
    {
        (await Verificar(Concessao(recursoTipoRestricao: RecursoTipo), Recurso()))
            .Passou.Should().BeTrue();

        (await Verificar(Concessao(recursoTipoRestricao: "OutroTipo"), Recurso()))
            .Passou.Should().BeFalse();
    }

    [Fact(DisplayName = "Concessão sem fim de vigência permanece aplicável")]
    public async Task Check_ConcessaoSemValidade_Aplicavel()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: null), Recurso());

        resultado.Passou.Should().BeTrue("a concessão sem prazo vale até ser revogada");
    }

    [Fact(DisplayName = "Concessão cujo fim de vigência é o instante do acesso já expirou, como em Concessao.EstaAtiva")]
    public async Task Check_ValidadeNoInstanteDoAcesso_Expirada()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: Agora), Recurso());

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão válida até um instante depois do acesso ainda vale")]
    public async Task Check_ValidadeLogoDepoisDoAcesso_Aplicavel()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: Agora.AddTicks(1)), Recurso());

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão expirada de mesma permissão nega por concessao_expirada")]
    public async Task Check_ConcessaoExpirada_NegaPorExpiracao()
    {
        CheckResult resultado = await Verificar(
            Concessao(validoAte: Agora.AddTicks(-1)),
            Recurso());

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão expirada não encobre outra concessão válida")]
    public async Task Check_ExpiradaEValida_SelecionaAValida()
    {
        CheckResult resultado = await Verificar(
            [
                Concessao(validoAte: Agora.AddDays(-1)),
                Concessao(),
            ],
            Recurso());

        resultado.Passou.Should().BeTrue();
        resultado.GrantSelecionado!.ValidoAte.Should().BeNull();
    }

    [Fact(DisplayName = "Entre concessões aplicáveis vence a mais específica")]
    public async Task Check_VariasAplicaveis_SelecionaAMaisEspecifica()
    {
        EffectiveGrant geral = Concessao(grantId: Guid.CreateVersion7());
        EffectiveGrant porProcesso = Concessao(grantId: Guid.CreateVersion7(), escopoProcessoId: Processo);

        CheckResult resultado = await Verificar(
            [geral, porProcesso],
            Recurso(processoId: Processo));

        resultado.GrantSelecionado.Should().Be(porProcesso,
            "sem critério, a trilha registraria ora uma concessão, ora outra");
    }

    [Fact(DisplayName = "Empate de especificidade resolve pela primeira concessão da lista")]
    public async Task Check_EmpateDeEspecificidade_SelecionaAPrimeira()
    {
        EffectiveGrant primeira = Concessao(grantId: Guid.CreateVersion7());
        EffectiveGrant segunda = Concessao(grantId: Guid.CreateVersion7());

        CheckResult resultado = await Verificar([primeira, segunda], Recurso());

        resultado.GrantSelecionado.Should().Be(primeira);
    }

    [Fact(DisplayName = "A comparação do código da permissão é estrita quanto a maiúsculas")]
    public async Task Check_CodigoComGrafiaDiferente_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao("Configuracao:Motivos-Decisao-Recursal:Manter"),
            Recurso());

        resultado.Passou.Should().BeFalse(
            "o catálogo fixa o código em minúsculas; aceitar variantes tornaria a concessão "
            + "dependente da grafia com que alguém a digitou no provedor de identidade");
    }

    private Task<CheckResult> Verificar(EffectiveGrant concessao, ResourceContext recurso)
        => Verificar([concessao], recurso);

    private Task<CheckResult> Verificar(EffectiveGrant[] concessoes, ResourceContext recurso)
        => _check.CheckAsync(
            Sujeito(concessoes: concessoes),
            Requisito(),
            recurso,
            Requisicao(),
            CancellationToken.None);
}
