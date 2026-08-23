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

    [Fact(DisplayName = "Concessão global alcança recurso de qualquer escopo")]
    public async Task Check_ConcessaoSemEscopo_AlcancaQualquerRecurso()
    {
        CheckResult resultado = await Verificar(
            Concessao(),
            Recurso(unidadeProprietariaId: Unidade, processoId: Processo));

        resultado.Passou.Should().BeTrue();
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

    [Fact(DisplayName = "Concessão do token sem validade própria permanece aplicável")]
    public async Task Check_ConcessaoDeTokenSemValidade_Aplicavel()
    {
        CheckResult resultado = await Verificar(Concessao(validoAte: null), Recurso());

        resultado.Passou.Should().BeTrue("a concessão do token herda a validade do próprio token");
    }

    [Fact(DisplayName = "Concessão válida exatamente no instante do acesso ainda vale")]
    public async Task Check_ValidadeNoInstanteDoAcesso_Aplicavel()
    {
        CheckResult resultado = await Verificar(
            Concessao(fonte: FonteGrant.OidcGroupBinding, validoAte: Agora),
            Recurso());

        resultado.Passou.Should().BeTrue("a validade é até o instante, inclusive");
    }

    [Fact(DisplayName = "Concessão expirada de mesma permissão nega por concessao_expirada")]
    public async Task Check_ConcessaoExpirada_NegaPorExpiracao()
    {
        CheckResult resultado = await Verificar(
            Concessao(fonte: FonteGrant.OidcGroupBinding, validoAte: Agora.AddTicks(-1)),
            Recurso());

        resultado.Motivo.Should().Be(MotivoNegativa.ConcessaoExpirada);
    }

    [Fact(DisplayName = "Concessão expirada não encobre outra concessão válida")]
    public async Task Check_ExpiradaEValida_SelecionaAValida()
    {
        CheckResult resultado = await Verificar(
            [
                Concessao(fonte: FonteGrant.OidcGroupBinding, validoAte: Agora.AddDays(-1)),
                Concessao(),
            ],
            Recurso());

        resultado.Passou.Should().BeTrue();
        resultado.GrantSelecionado!.ValidoAte.Should().BeNull();
    }

    [Fact(DisplayName = "Entre concessões aplicáveis vence a mais específica")]
    public async Task Check_VariasAplicaveis_SelecionaAMaisEspecifica()
    {
        EffectiveGrant global = Concessao(grantId: Guid.CreateVersion7());
        EffectiveGrant porUnidade = Concessao(grantId: Guid.CreateVersion7(), escopoUnidadeId: Unidade);

        CheckResult resultado = await Verificar(
            [global, porUnidade],
            Recurso(unidadeProprietariaId: Unidade));

        resultado.GrantSelecionado.Should().Be(porUnidade,
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
