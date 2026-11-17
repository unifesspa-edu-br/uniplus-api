namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;

public sealed class EffectiveGrantDoVinculoDeGrupoTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly DateTimeOffset Validade = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "O vínculo de grupo guarda a fonte, a validade, o identificador e o escopo")]
    public void DoVinculoDeGrupo_DadosValidos_GuardaOQueRecebeu()
    {
        Guid grantId = Guid.CreateVersion7();

        EffectiveGrant grant = EffectiveGrant.DoVinculoDeGrupo(
            Permissao, EscopoDaPermissaoEfetiva.Instituicao, Validade, grantId).Value!;

        (grant.Fonte, grant.ValidoAte, grant.GrantId).Should().Be((FonteGrant.OidcGroupBinding, Validade, grantId));
        grant.Escopo.Should().Be(EscopoDaPermissaoEfetiva.Instituicao);
        (grant.PerfilId, grant.ConcedidoPor).Should().Be(((Guid?)null, (UsuarioRef?)null));
    }

    [Fact(DisplayName = "O vínculo de grupo sem validade é recusado")]
    public void DoVinculoDeGrupo_SemValidade_Recusa() =>
        EffectiveGrant.DoVinculoDeGrupo(Permissao, EscopoDaPermissaoEfetiva.Instituicao, null).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria);
}
