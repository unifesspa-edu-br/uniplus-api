namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Cobre o CA-01 da Story #608 (records imutáveis, igualdade por valor) para os
/// tipos da assinatura sem coleção, onde a igualdade por valor é determinística.
/// A varredura por reflection de todo o contrato (sem setter público, schema
/// canônico, sem nome de provedor — CA-10/CA-11/CA-07) é a fitness de contrato
/// da task irmã #653.
/// </summary>
public sealed class TiposFormaisContratoTests
{
    [Fact]
    public void TiposFormais_Imutaveis_IgualdadePorValor()
    {
        // DenyReason — igualdade por código.
        DenyReason.De(MotivoNegativa.FaseFechada)
            .Should().Be(DenyReason.De(MotivoNegativa.FaseFechada));
        DenyReason.De(MotivoNegativa.FaseFechada)
            .Should().NotBe(DenyReason.De(MotivoNegativa.ConcessaoExpirada));

        // ResourceContext — igualdade por valor dos atributos.
        Guid unidade = Guid.CreateVersion7();
        ResourceContext recursoA = ResourceContext.From("Edital", Sensibilidade.Pessoal, unidadeProprietariaId: unidade).Value!;
        ResourceContext recursoB = ResourceContext.From("Edital", Sensibilidade.Pessoal, unidadeProprietariaId: unidade).Value!;
        recursoA.Should().Be(recursoB);
        recursoA.Should().NotBe(ResourceContext.From("Inscricao", Sensibilidade.Pessoal, unidadeProprietariaId: unidade).Value!);

        // AuthorizationRequestContext — igualdade por valor (sem dupla aprovação).
        var instante = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        AuthorizationRequestContext reqA = AuthorizationRequestContext.From("req-1", instante, OrigemRequisicao.Api).Value!;
        AuthorizationRequestContext reqB = AuthorizationRequestContext.From("req-1", instante, OrigemRequisicao.Api).Value!;
        reqA.Should().Be(reqB);

        // AuthorizationDecision — permitida igual por valor do grant; negada por código.
        EffectiveGrant grant1 = EffectiveGrant.From("selecao.editais.publicar", FonteGrant.Token).Value!;
        EffectiveGrant grant2 = EffectiveGrant.From("selecao.editais.publicar", FonteGrant.Token).Value!;
        AuthorizationDecision.Permitir(grant1).Should().Be(AuthorizationDecision.Permitir(grant2));
        AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel)
            .Should().Be(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));

        // Permitida e negada nunca são iguais.
        AuthorizationDecision.Permitir(grant1)
            .Should().NotBe(AuthorizationDecision.Negar(MotivoNegativa.SemConcessaoAplicavel));
    }

    // Tipos com coleção: o Equals sintetizado pelo record compararia as coleções
    // por referência. O Equals/GetHashCode customizado restaura a igualdade por
    // valor exigida pelo CA-01 mesmo com instâncias de coleção distintas.

    [Fact]
    public void PermissionRequirement_ConteudoIdentico_EhIgualPorValor()
    {
        PermissionRequirement Criar() => PermissionRequirement.From(
            "selecao.resultado.homologar",
            Sensibilidade.Sensivel,
            baseLegalPadrao: "LGPD art. 7º, II",
            requerMfa: true,
            escopoContextoObrigatorio: ["processoId"],
            verificacoesDeContexto: ["fase_aberta", "base_legal"]).Value!;

        PermissionRequirement a = Criar();
        PermissionRequirement b = Criar();

        a.Should().Be(b, "coleções de conteúdo idêntico não quebram a igualdade por valor");
        a.GetHashCode().Should().Be(b.GetHashCode());

        PermissionRequirement diferente = PermissionRequirement.From(
            "selecao.resultado.homologar",
            Sensibilidade.Sensivel,
            verificacoesDeContexto: ["fase_aberta"]).Value!;
        a.Should().NotBe(diferente, "conteúdo de coleção distinto torna os contratos desiguais");
    }

    [Fact]
    public void AuthorizationSubject_ConteudoIdentico_EhIgualPorValor()
    {
        UsuarioRef usuario = UsuarioRef.From("https://sso.gov.br", "sub-1").Value!;
        Guid unidade = Guid.CreateVersion7();
        EffectiveGrant grant = EffectiveGrant.From("selecao.editais.publicar", FonteGrant.Token).Value!;

        AuthorizationSubject Criar(IEnumerable<string> grupos) => AuthorizationSubject.From(
            usuario,
            "jti-1",
            gruposOidc: grupos,
            unidadesAdministradas: [unidade],
            concessoesEfetivas: [grant]).Value!;

        AuthorizationSubject a = Criar(["ceps-admin", "crca-leitor"]);
        AuthorizationSubject b = Criar(["crca-leitor", "ceps-admin"]); // mesma composição, ordem distinta

        a.Should().Be(b, "conjuntos comparam-se por conteúdo, não por ordem nem referência");
        a.GetHashCode().Should().Be(b.GetHashCode());

        AuthorizationSubject diferente = Criar(["ceps-admin"]);
        a.Should().NotBe(diferente, "composição de grupos distinta torna os sujeitos desiguais");
    }
}
