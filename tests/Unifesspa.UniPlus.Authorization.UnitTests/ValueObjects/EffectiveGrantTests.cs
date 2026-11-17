namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que vale para a concessão efetiva de toda origem: a permissão obrigatória e aparada, o identificador da concessão e
/// a permissão e o identificador da concessão recusados isoladamente. A ordem entre dois defeitos está em EffectiveGrantPrecedenciaDasRecusasTests.
/// </summary>
public sealed class EffectiveGrantTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly DateTimeOffset Validade = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly EscopoDaPermissaoEfetiva DaUnidade = EscopoDaPermissaoEfetiva.DaUnidade(Guid.CreateVersion7()).Value!;

    public static TheoryData<FonteGrant, string?> FontesEPermissoesEmBranco()
    {
        TheoryData<FonteGrant, string?> dados = [];
        foreach (FonteGrant fonte in Enum.GetValues<FonteGrant>())
        {
            dados.Add(fonte, null);
            dados.Add(fonte, string.Empty);
            dados.Add(fonte, "   ");
        }

        return dados;
    }

    private static Result<EffectiveGrant> Montar(FonteGrant fonte, string? permissao, Guid? grantId = null) =>
        fonte switch
        {
            FonteGrant.Concessao => EffectiveGrant.DaConcessaoDePerfil(permissao, EscopoDaPermissaoEfetiva.Instituicao, grantId),
            FonteGrant.OidcGroupBinding => EffectiveGrant.DoVinculoDeGrupo(permissao, DaUnidade, Validade, grantId),
            FonteGrant.PermissaoExcecional => EffectiveGrant.Excepcional(permissao, DaUnidade, Validade, grantId: grantId),
            _ => EffectiveGrant.PorTitularidade(permissao),
        };

    [Theory(DisplayName = "A permissão em branco é recusada em toda origem")]
    [MemberData(nameof(FontesEPermissoesEmBranco))]
    public void Fabrica_PermissaoEmBranco_Recusa(FonteGrant fonte, string? permissao) =>
        Montar(fonte, permissao).Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);

    [Theory(DisplayName = "O identificador da concessão vazio é recusado nas origens que o recebem")]
    [InlineData(FonteGrant.Concessao)]
    [InlineData(FonteGrant.OidcGroupBinding)]
    [InlineData(FonteGrant.PermissaoExcecional)]
    public void Fabrica_GrantIdGuidVazio_Recusa(FonteGrant fonte) =>
        Montar(fonte, Permissao, Guid.Empty).Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);

    [Theory(DisplayName = "A permissão é guardada sem os espaços das extremidades em toda origem")]
    [InlineData(FonteGrant.Concessao)]
    [InlineData(FonteGrant.OidcGroupBinding)]
    [InlineData(FonteGrant.PermissaoExcecional)]
    [InlineData(FonteGrant.Titularidade)]
    public void Fabrica_PermissaoComEspacos_GuardaAparada(FonteGrant fonte)
    {
        EffectiveGrant grant = Montar(fonte, $"  {Permissao}\t").Value!;

        grant.PermissaoCodigo.Should().Be(Permissao);
        grant.Fonte.Should().Be(fonte);
    }
}
