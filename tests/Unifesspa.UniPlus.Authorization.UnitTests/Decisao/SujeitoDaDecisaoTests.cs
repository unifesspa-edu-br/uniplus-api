namespace Unifesspa.UniPlus.Authorization.UnitTests.Decisao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

public sealed class SujeitoDaDecisaoTests
{
    [Fact]
    public void Sujeito_Campos_SaoLidosComOsTiposDoContrato()
    {
        AuthorizationSubject sujeito = CenariosDeDecisao.Sujeito();
        UsuarioRef usuario = sujeito.Usuario;
        IReadOnlySet<string> grupos = sujeito.GruposOidc;
        IReadOnlySet<Guid> unidades = sujeito.UnidadesAdministradas;
        IReadOnlyList<EscopoAuditoriaVigente> escopos = sujeito.EscoposAuditoria;

        usuario.Should().NotBeNull();
        grupos.Should().BeEmpty();
        unidades.Should().BeEmpty();
        escopos.Should().BeEmpty();
    }

    [Fact]
    public void Sujeito_SessaoEConcessoes_SaoLidasComOsTiposDoContrato()
    {
        AuthorizationSubject sujeito = AuthorizationSubject.From(
            UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "conta-opaco-123").Value!,
            "jti-abc", atuacaoAtiva: null).Value!;
        AtuacaoVigente? atuacao = sujeito.AtuacaoAtiva;
        bool mfa = sujeito.MfaSatisfeito;
        string jti = sujeito.Jti;
        IReadOnlyList<EffectiveGrant> concessoes = sujeito.ConcessoesEfetivas;

        atuacao.Should().BeNull();
        mfa.Should().BeFalse();
        jti.Should().Be("jti-abc");
        concessoes.Should().BeEmpty();
    }
}
