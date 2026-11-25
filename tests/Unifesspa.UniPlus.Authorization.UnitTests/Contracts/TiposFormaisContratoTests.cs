namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Confere a igualdade por valor dos tipos da assinatura sem coleção, onde ela é determinística.
/// A imutabilidade dos records do contrato é conferida pela regra de arquitetura
/// ContratoDeAutorizacaoImutavelTests.
/// </summary>
public sealed class TiposFormaisContratoTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly Guid Unidade = Guid.CreateVersion7();

    private static EffectiveGrant ConcessaoInstitucional() =>
        EffectiveGrant.DaConcessaoDePerfil(Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value!;

    private static ResourceContext Resource(string tipo) =>
        ResourceContext.From(tipo, ClassificacaoDeProtecao.Pessoal, unidadeProprietariaId: Unidade).Value!;

    private static AuthorizationRequestContext Requisicao(Guid execucao) =>
        AuthorizationRequestContext.From(
            "req-1", new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero), OrigemRequisicao.Api, execucaoId: execucao).Value!;

    private static PermissionRequirement Requisito(IEnumerable<string> verificacoes) =>
        PermissionRequirement.From(
            "selecao.resultado.homologar",
            ClassificacaoDeProtecao.Sensivel,
            baseLegalPadrao: "LGPD art. 7º, II",
            requerMfa: true,
            escopoContextoObrigatorio: ["processoId"],
            verificacoesDeContexto: verificacoes).Value!;

    private static AuthorizationSubject Sujeito(IEnumerable<string> grupos) =>
        AuthorizationSubject.From(
            UsuarioRef.From("https://sso.gov.br", "conta-1").Value!,
            "jti-1",
            gruposOidc: grupos,
            unidadesAdministradas: [Unidade],
            concessoesEfetivas: [ConcessaoInstitucional()]).Value!;

    [Fact]
    public void DenyReason_MesmoMotivo_EhIgualPorValor()
    {
        DenyReason.De(MotivoNegativa.FaseFechada).Should().Be(DenyReason.De(MotivoNegativa.FaseFechada));
        DenyReason.De(MotivoNegativa.FaseFechada).Should().NotBe(DenyReason.De(MotivoNegativa.ConcessaoExpirada));
    }

    [Fact]
    public void ResourceContext_MesmosAtributos_EhIgualPorValor()
    {
        Resource("edital").Should().Be(Resource("edital"));
        Resource("edital").Should().NotBe(Resource("inscricao"));
    }

    [Fact]
    public void AuthorizationRequestContext_MesmosAtributos_EhIgualPorValor()
    {
        Guid execucao = Guid.CreateVersion7();

        Requisicao(execucao).Should().Be(Requisicao(execucao));
    }

    [Fact]
    public void AuthorizationDecision_PermitidaIgualPorGrantENegadaPorCodigo()
    {
        AuthorizationDecision.Permitir(ConcessaoInstitucional()).Should().Be(AuthorizationDecision.Permitir(ConcessaoInstitucional()));
        AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel)
            .Should().Be(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));
        AuthorizationDecision.Permitir(ConcessaoInstitucional())
            .Should().NotBe(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));
    }

    // Tipos com coleção: o Equals sintetizado pelo record compararia as coleções
    // por referência. O Equals/GetHashCode customizado restaura a igualdade por
    // valor exigida pelo CA-01 mesmo com instâncias de coleção distintas.

    [Fact]
    public void PermissionRequirement_ConteudoIdentico_EhIgualPorValor()
    {
        PermissionRequirement a = Requisito(["fase_aberta", "base_legal"]);
        PermissionRequirement b = Requisito(["fase_aberta", "base_legal"]);

        a.Should().Be(b, "coleções de conteúdo idêntico não quebram a igualdade por valor");
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(Requisito(["fase_aberta"]), "conteúdo de coleção distinto torna os contratos desiguais");
    }

    [Fact]
    public void AuthorizationSubject_ConteudoIdentico_EhIgualPorValor()
    {
        AuthorizationSubject a = Sujeito(["ceps-admin", "proeg-leitor"]);
        AuthorizationSubject b = Sujeito(["proeg-leitor", "ceps-admin"]); // mesma composição, ordem distinta

        a.Should().Be(b, "conjuntos comparam-se por conteúdo, não por ordem nem referência");
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(Sujeito(["ceps-admin"]), "composição de grupos distinta torna os sujeitos desiguais");
    }
}
