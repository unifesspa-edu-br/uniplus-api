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

    private static UsuarioRef Usuario() => UsuarioRef.From(Emissor, "sub-123").Value!;

    // ─── CA-03: grants de fontes distintas convivem numa única lista ───────

    [Fact]
    public void AuthorizationSubject_GrantsDeFontesDistintas_ConvivemNaLista()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        EffectiveGrant doToken = EffectiveGrant.From(Permissao, FonteGrant.Token).Value!;
        EffectiveGrant doGrupo = EffectiveGrant.From(Permissao, FonteGrant.OidcGroupBinding, validoAte: validade).Value!;
        EffectiveGrant excepcional = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, escopoUnidadeId: Guid.CreateVersion7(), validoAte: validade).Value!;

        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(
            Usuario(), "jti-abc", concessoesEfetivas: [doToken, doGrupo, excepcional]);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ConcessoesEfetivas
            .Select(g => g.Fonte)
            .Should().BeEquivalentTo([FonteGrant.Token, FonteGrant.OidcGroupBinding, FonteGrant.PermissaoExcecional],
                "as três fontes rastreáveis convivem na mesma lista somente-leitura");
    }

    [Fact]
    public void AuthorizationSubject_CamposCanonicos_SaoPreservados()
    {
        UsuarioRef usuario = Usuario();
        Guid unidade = Guid.CreateVersion7();
        EscopoAuditoriaVigente escopo = EscopoAuditoriaVigente.From(
            Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddDays(1)).Value!;
        AtuacaoVigente atuacao = AtuacaoVigente.From(unidade, DateTimeOffset.UtcNow.AddHours(2)).Value!;

        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(
            usuario,
            "jti-xyz",
            mfaSatisfeito: true,
            gruposOidc: ["ceps-admin", "ceps-admin"],
            unidadesAdministradas: [unidade],
            escoposAuditoria: [escopo],
            atuacaoAtiva: atuacao);

        resultado.IsSuccess.Should().BeTrue();
        AuthorizationSubject subject = resultado.Value!;
        subject.Usuario.Should().Be(usuario);
        subject.Jti.Should().Be("jti-xyz");
        subject.MfaSatisfeito.Should().BeTrue();
        subject.GruposOidc.Should().ContainSingle("o conjunto remove a duplicata").Which.Should().Be("ceps-admin");
        subject.UnidadesAdministradas.Should().ContainSingle().Which.Should().Be(unidade);
        subject.EscoposAuditoria.Should().ContainSingle().Which.Should().Be(escopo);
        subject.AtuacaoAtiva.Should().Be(atuacao);
    }

    [Fact]
    public void AuthorizationSubject_CopiaDefensivaDeColecoes_NaoRefleteMutacaoExterna()
    {
        var concessoes = new List<EffectiveGrant> { EffectiveGrant.From(Permissao, FonteGrant.Token).Value! };

        Result<AuthorizationSubject> resultado = AuthorizationSubject.From(
            Usuario(), "jti-abc", concessoesEfetivas: concessoes);

        concessoes.Add(EffectiveGrant.From("outra.permissao", FonteGrant.Token).Value!); // muta a origem

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
