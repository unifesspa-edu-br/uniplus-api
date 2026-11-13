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

    [Fact(DisplayName = "Concessão institucional alcança resource sem unidade proprietária")]
    public async Task Check_InstitucionalEmResourceSemUnidade_Alcanca()
    {
        CheckResult resultado = await Verificar(Concessao(), Resource(processoId: Processo));

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão institucional não alcança resource de uma unidade, sem herança")]
    public async Task Check_InstitucionalEmResourceDeUnidade_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(Concessao(), Resource(unidadeProprietariaId: Unidade));

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão de unidade não alcança resource institucional")]
    public async Task Check_UnidadeEmResourceSemUnidade_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(Concessao(escopoUnidadeId: Unidade), Resource());

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão expirada na unidade A não encobre a vigente na unidade B")]
    public async Task Check_ExpiradaEmOutraUnidade_NaoEncobreAVigente()
    {
        EffectiveGrant vigente = Concessao(grantId: Guid.CreateVersion7(), escopoUnidadeId: OutraUnidade);

        CheckResult resultado = await Verificar(
            [Concessao(escopoUnidadeId: Unidade, validoAte: Agora.AddDays(-1)), vigente],
            Resource(unidadeProprietariaId: OutraUnidade));

        resultado.GrantSelecionado.Should().Be(vigente);
    }

    [Fact(DisplayName = "Concessão vencida na unidade do resource nega por concessao_expirada")]
    public async Task Check_VencidaNaUnidadeDoResource_NegaPorExpiracao()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade, validoAte: Agora.AddDays(-1)),
            Resource(unidadeProprietariaId: Unidade));

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão por unidade alcança resource da mesma unidade")]
    public async Task Check_EscopoDeUnidadeIgual_Alcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade),
            Resource(unidadeProprietariaId: Unidade));

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão por unidade não alcança resource de outra unidade")]
    public async Task Check_EscopoDeUnidadeDiferente_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoUnidadeId: Unidade),
            Resource(unidadeProprietariaId: OutraUnidade));

        resultado.Passou.Should().BeFalse();
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
    }

    [Fact(DisplayName = "Concessão com escopo não alcança resource sem aquele escopo")]
    public async Task Check_ResourceSemEscopoQueAConcessaoRestringe_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao(escopoProcessoId: Processo),
            Resource(processoId: null));

        resultado.Passou.Should().BeFalse(
            "a ausência do escopo no resource não relaxa a restrição da concessão");
    }

    [Fact(DisplayName = "Restrição por tipo de resource alcança apenas o tipo restrito")]
    public async Task Check_RestricaoDeTipo_AlcancaSomenteOTipo()
    {
        (await Verificar(Concessao(resourceTipoRestricao: ResourceTipo), Resource()))
            .Passou.Should().BeTrue();

        (await Verificar(Concessao(resourceTipoRestricao: "outro-tipo"), Resource()))
            .Passou.Should().BeFalse();
    }

    [Fact(DisplayName = "Concessão sem fim de vigência permanece aplicável")]
    public async Task Check_ConcessaoSemValidade_Aplicavel()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: null), Resource());

        resultado.Passou.Should().BeTrue("a concessão sem prazo vale até ser revogada");
    }

    [Fact(DisplayName = "Concessão cujo fim de vigência é o instante do acesso já expirou, como em Concessao.EstaAtiva")]
    public async Task Check_ValidadeNoInstanteDoAcesso_Expirada()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: Agora), Resource());

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão válida até um instante depois do acesso ainda vale")]
    public async Task Check_ValidadeLogoDepoisDoAcesso_Aplicavel()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: Agora.AddTicks(1)), Resource());

        resultado.Passou.Should().BeTrue();
    }

    [Fact(DisplayName = "Concessão expirada de mesma permissão nega por concessao_expirada")]
    public async Task Check_ConcessaoExpirada_NegaPorExpiracao()
    {
        CheckResult resultado = await Verificar(
            Concessao(validoAte: Agora.AddTicks(-1)),
            Resource());

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
            Resource());

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
            Resource(processoId: Processo));

        resultado.GrantSelecionado.Should().Be(porProcesso,
            "sem critério, a trilha registraria ora uma concessão, ora outra");
    }

    [Fact(DisplayName = "Empate de especificidade resolve pela primeira concessão da lista")]
    public async Task Check_EmpateDeEspecificidade_SelecionaAPrimeira()
    {
        EffectiveGrant primeira = Concessao(grantId: Guid.CreateVersion7());
        EffectiveGrant segunda = Concessao(grantId: Guid.CreateVersion7());

        CheckResult resultado = await Verificar([primeira, segunda], Resource());

        resultado.GrantSelecionado.Should().Be(primeira);
    }

    [Fact(DisplayName = "A comparação do código da permissão é estrita quanto a maiúsculas")]
    public async Task Check_CodigoComGrafiaDiferente_NaoAlcanca()
    {
        CheckResult resultado = await Verificar(
            Concessao("Configuracao:Motivos-Decisao-Recursal:Manter"),
            Resource());

        resultado.Passou.Should().BeFalse(
            "o catálogo fixa o código em minúsculas; aceitar variantes tornaria a concessão "
            + "dependente da grafia com que alguém a digitou no provedor de identidade");
    }

    [Fact(DisplayName = "A titularidade com escopo institucional e a permissão pedida é recusada: não é concessão e nunca autoriza")]
    public async Task Check_Titularidade_RecusaComFalhaFechada()
    {
        CheckResult resultado = await Verificar(Concessao(fonte: FonteGrant.Titularidade), Resource());

        resultado.Passou.Should().BeFalse("a titularidade só existe no registro da leitura da parte (ADR-0166)");
        resultado.Motivo.Should().Be(MotivoNegativa.SemConcessaoAplicavel);
        resultado.GrantSelecionado.Should().BeNull();
    }

    private Task<CheckResult> Verificar(EffectiveGrant concessao, ResourceContext resource)
        => Verificar([concessao], resource);

    private Task<CheckResult> Verificar(EffectiveGrant[] concessoes, ResourceContext resource)
        => _check.CheckAsync(
            Sujeito(concessoes: concessoes),
            Requisito(),
            resource,
            Requisicao(),
            CancellationToken.None);
}
