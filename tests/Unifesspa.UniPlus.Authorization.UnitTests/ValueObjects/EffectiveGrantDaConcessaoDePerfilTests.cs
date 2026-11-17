namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EffectiveGrantDaConcessaoDePerfilTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly DateTimeOffset Validade = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static EscopoDaPermissaoEfetiva Escopo(Guid? unidade, Guid? processo, Guid? chamada, string? tipo, bool institucional) =>
        EscopoDaPermissaoEfetiva.Criar(unidade, processo, chamada, tipo, institucional).Value!;

    [Fact(DisplayName = "A concessão de perfil guarda a fonte, o identificador, o perfil, a validade e o escopo")]
    public void DaConcessaoDePerfil_DadosValidos_GuardaOQueRecebeu()
    {
        Guid grantId = Guid.CreateVersion7();
        Guid perfilId = Guid.CreateVersion7();
        EscopoDaPermissaoEfetiva escopo = EscopoDaPermissaoEfetiva.DaUnidade(Guid.CreateVersion7()).Value!;

        EffectiveGrant grant = EffectiveGrant.DaConcessaoDePerfil(Permissao, escopo, grantId, perfilId, Validade).Value!;

        (grant.Fonte, grant.GrantId, grant.PerfilId, grant.ValidoAte).Should().Be((FonteGrant.Concessao, grantId, perfilId, Validade));
        grant.Escopo.Should().Be(escopo);
        (grant.ConcedidoPor, grant.VersaoId).Should().Be(((UsuarioRef?)null, (Guid?)null));
    }

    [Fact(DisplayName = "A concessão de perfil aceita ficar sem validade")]
    public void DaConcessaoDePerfil_SemValidade_Aceita() =>
        EffectiveGrant.DaConcessaoDePerfil(Permissao, EscopoDaPermissaoEfetiva.Instituicao).Value!.ValidoAte.Should().BeNull();

    [Theory(DisplayName = "A concessão de perfil recusa o escopo sem unidade nem instituição, ainda que tenha processo, chamada ou tipo")]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void DaConcessaoDePerfil_SemUnidadeNemInstituicao_Recusa(bool comProcesso, bool comChamada, bool comTipo)
    {
        EscopoDaPermissaoEfetiva escopo = Escopo(
            null, comProcesso ? Guid.CreateVersion7() : null, comChamada ? Guid.CreateVersion7() : null, comTipo ? "edital" : null, false);

        Result<EffectiveGrant> resultado = EffectiveGrant.DaConcessaoDePerfil(Permissao, escopo);

        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoObrigatorio);
    }

    [Fact(DisplayName = "A concessão de perfil recusa o escopo da unidade e da instituição juntas")]
    public void DaConcessaoDePerfil_UnidadeEInstituicaoJuntas_Recusa() =>
        EffectiveGrant.DaConcessaoDePerfil(Permissao, Escopo(Guid.CreateVersion7(), null, null, null, true)).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoAmbiguo);

    [Fact(DisplayName = "A concessão de perfil aceita processo, chamada e tipo de resource além da instituição")]
    public void DaConcessaoDePerfil_InstituicaoComProcessoChamadaETipo_Aceita()
    {
        EscopoDaPermissaoEfetiva escopo = Escopo(null, Guid.CreateVersion7(), Guid.CreateVersion7(), "edital", true);

        EffectiveGrant grant = EffectiveGrant.DaConcessaoDePerfil(Permissao, escopo).Value!;

        grant.Escopo.Should().Be(escopo);
    }

    [Fact(DisplayName = "A concessão de perfil exige o escopo")]
    public void DaConcessaoDePerfil_EscopoNulo_Falha()
    {
        Func<Result<EffectiveGrant>> acao = () => EffectiveGrant.DaConcessaoDePerfil(Permissao, null!);

        acao.Should().Throw<ArgumentNullException>();
    }
}
