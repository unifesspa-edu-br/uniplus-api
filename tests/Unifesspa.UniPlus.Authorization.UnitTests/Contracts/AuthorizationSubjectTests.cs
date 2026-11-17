namespace Unifesspa.UniPlus.Authorization.UnitTests.Contracts;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class AuthorizationSubjectTests
{
    private const string Emissor = "https://sso.gov.br";
    private const string Permissao = "selecao.editais.publicar";

    private static readonly Guid UnidadeAdministrada = Guid.CreateVersion7();

    private static readonly EscopoAuditoriaVigente EscopoDeAuditoria =
        EscopoAuditoriaVigente.From(Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddDays(1)).Value!;

    private static readonly AtuacaoVigente Atuacao = AtuacaoVigente.From(UnidadeAdministrada, DateTimeOffset.UtcNow.AddHours(2)).Value!;

    private static EffectiveGrant[] GrantsDeTodasAsFontes()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);
        EscopoDaPermissaoEfetiva daUnidade = EscopoDaPermissaoEfetiva.DaUnidade(Guid.CreateVersion7()).Value!;

        return
        [
            EffectiveGrant.DaConcessaoDePerfil(Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value!,
            EffectiveGrant.DoVinculoDeGrupo(Permissao, EscopoDaPermissaoEfetiva.Instituicao, validade).Value!,
            EffectiveGrant.Excepcional(Permissao, daUnidade, validade).Value!,
        ];
    }

    private static UsuarioRef Usuario() => UsuarioRef.From(Emissor, "conta-123").Value!;

    // ─── CA-03: grants de fontes distintas convivem numa única lista ───────

    [Fact]
    public void AuthorizationSubject_GrantsDeFontesDistintas_ConvivemNaLista()
    {
        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(
            Usuario(), "jti-abc", concessoesEfetivas: GrantsDeTodasAsFontes());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConcessoesEfetivas
            .Select(grant => grant.Fonte)
            .Should().BeEquivalentTo([FonteGrant.Concessao, FonteGrant.OidcGroupBinding, FonteGrant.PermissaoExcecional],
                "as três fontes rastreáveis convivem na mesma lista somente-leitura");
    }

    [Fact]
    public void AuthorizationSubject_CamposCanonicos_SaoPreservados()
    {
        UsuarioRef usuario = Usuario();

        AuthorizationSubject subject = AuthorizationSubject.From(
            usuario, "jti-xyz", mfaSatisfeito: true, gruposOidc: ["ceps-admin", "ceps-admin"],
            unidadesAdministradas: [UnidadeAdministrada], escoposAuditoria: [EscopoDeAuditoria], atuacaoAtiva: Atuacao).Value!;

        (subject.Usuario, subject.Jti, subject.MfaSatisfeito).Should().Be((usuario, "jti-xyz", true));
        subject.GruposOidc.Should().ContainSingle("o conjunto remove a duplicata").Which.Should().Be("ceps-admin");
        subject.UnidadesAdministradas.Should().ContainSingle().Which.Should().Be(UnidadeAdministrada);
        subject.EscoposAuditoria.Should().ContainSingle().Which.Should().Be(EscopoDeAuditoria);
        subject.AtuacaoAtiva.Should().Be(Atuacao);
    }

    [Fact]
    public void AuthorizationSubject_CopiaDefensivaDeColecoes_NaoRefleteMutacaoExterna()
    {
        var concessoes = new List<EffectiveGrant> { EffectiveGrant.DaConcessaoDePerfil(Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value! };

        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(
            Usuario(), "jti-abc", concessoesEfetivas: concessoes);

        concessoes.Add(EffectiveGrant.DaConcessaoDePerfil("outra.permissao", EscopoDaPermissaoEfetiva.Instituicao).Value!); // muta a origem

        resultado.Value!.ConcessoesEfetivas.Should().ContainSingle("a cópia é defensiva e imutável");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizationSubject_JtiVazio_Rejeita(string? jti)
    {
        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(Usuario(), jti);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.AuthorizationSubjectJtiObrigatorio);
    }

    [Fact]
    public void AuthorizationSubject_ColecoesNulas_ViramVaziasNaoNulas()
    {
        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(Usuario(), "jti-abc");

        resultado.IsSuccess.Should().BeTrue();
        AuthorizationSubject subject = resultado.Value!;
        subject.GruposOidc.Should().BeEmpty();
        subject.UnidadesAdministradas.Should().BeEmpty();
        subject.EscoposAuditoria.Should().BeEmpty();
        subject.ConcessoesEfetivas.Should().BeEmpty();
        subject.AtuacaoAtiva.Should().BeNull();
    }
}
