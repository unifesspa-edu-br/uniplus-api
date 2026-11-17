namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;

public sealed class EffectiveGrantExcepcionalTests
{
    private const string Permissao = "selecao.editais.publicar";

    private static readonly DateTimeOffset Validade = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static EscopoDaPermissaoEfetiva Escopo(Guid? unidade, Guid? processo, Guid? chamada, string? tipo, bool institucional) =>
        EscopoDaPermissaoEfetiva.Criar(unidade, processo, chamada, tipo, institucional).Value!;

    [Fact(DisplayName = "A concessão excepcional guarda a fonte, a validade, o concedente, o identificador e o escopo")]
    public void Excepcional_DadosValidos_GuardaOQueRecebeu()
    {
        UsuarioRef concedente = UsuarioRef.From("https://idp.exemplo/realms/unifesspa", "conta-opaco-1").Value!;
        Guid grantId = Guid.CreateVersion7();
        EscopoDaPermissaoEfetiva escopo = Escopo(null, Guid.CreateVersion7(), null, null, false);

        EffectiveGrant grant = EffectiveGrant.Excepcional(Permissao, escopo, Validade, concedente, grantId).Value!;

        (grant.Fonte, grant.ValidoAte, grant.ConcedidoPor, grant.GrantId).Should()
            .Be((FonteGrant.PermissaoExcecional, Validade, concedente, grantId));
        grant.Escopo.Should().Be(escopo);
    }

    [Fact(DisplayName = "A concessão excepcional sem validade é recusada")]
    public void Excepcional_SemValidade_Recusa() =>
        EffectiveGrant.Excepcional(Permissao, Escopo(Guid.CreateVersion7(), null, null, null, false), null).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria);

    [Theory(DisplayName = "A concessão excepcional sem unidade, processo, chamada nem tipo de resource é recusada")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(false, "   ")]
    public void Excepcional_SemRestricao_Recusa(bool institucional, string? tipo) =>
        EffectiveGrant.Excepcional(Permissao, Escopo(null, null, null, tipo, institucional), Validade).Error!.Code
            .Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoExcepcionalObrigatorio);

    [Theory(DisplayName = "A concessão excepcional aceita um só escopo")]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    [InlineData("resource")]
    public void Excepcional_ComUmEscopo_Aceita(string escopo)
    {
        EscopoDaPermissaoEfetiva restrito = Escopo(
            escopo == "unidade" ? Guid.CreateVersion7() : null,
            escopo == "processo" ? Guid.CreateVersion7() : null,
            escopo == "chamada" ? Guid.CreateVersion7() : null,
            escopo == "resource" ? "edital" : null,
            false);

        EffectiveGrant.Excepcional(Permissao, restrito, Validade).IsSuccess.Should().BeTrue();
    }
}
