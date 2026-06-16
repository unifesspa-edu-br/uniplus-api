namespace Unifesspa.UniPlus.Authorization.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EffectiveGrantTests
{
    private const string Permissao = "selecao.editais.publicar";

    // ─── Fonte rastreável é preservada ─────────────────────────────────────

    [Fact]
    public void EffectiveGrant_PreservaFonteRastreavel()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> token = EffectiveGrant.From(Permissao, FonteGrant.Token);
        Result<EffectiveGrant> oidc = EffectiveGrant.From(Permissao, FonteGrant.OidcGroupBinding, validoAte: validade);
        Result<EffectiveGrant> excepcional = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, escopoUnidadeId: Guid.CreateVersion7(), validoAte: validade);

        token.Value!.Fonte.Should().Be(FonteGrant.Token);
        oidc.Value!.Fonte.Should().Be(FonteGrant.OidcGroupBinding);
        excepcional.Value!.Fonte.Should().Be(FonteGrant.PermissaoExcecional);
    }

    [Fact]
    public void EffectiveGrant_PreservaEscoposERestricaoDeRecurso()
    {
        Guid unidade = Guid.CreateVersion7();
        Guid processo = Guid.CreateVersion7();
        Guid chamada = Guid.CreateVersion7();
        Guid grantId = Guid.CreateVersion7();

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.Token,
            grantId: grantId,
            escopoUnidadeId: unidade,
            escopoProcessoId: processo,
            escopoChamadaId: chamada,
            recursoTipoRestricao: "Edital");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.GrantId.Should().Be(grantId);
        resultado.Value.EscopoUnidadeId.Should().Be(unidade);
        resultado.Value.EscopoProcessoId.Should().Be(processo);
        resultado.Value.EscopoChamadaId.Should().Be(chamada);
        resultado.Value.RecursoTipoRestricao.Should().Be("Edital");
    }

    // ─── CA-03b: concessão server-side sem validade é rejeitada ────────────

    [Theory]
    [InlineData(FonteGrant.OidcGroupBinding)]
    [InlineData(FonteGrant.PermissaoExcecional)]
    public void EffectiveGrant_ServerSideSemValidade_Rejeita(FonteGrant fonte)
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, fonte);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria);
    }

    [Fact]
    public void EffectiveGrant_OidcGroupBindingComValidadeSemEscopo_Aceita()
    {
        // OidcGroupBinding pode representar permissão global — não exige escopo.
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(2);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, FonteGrant.OidcGroupBinding, validoAte: validade);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ValidoAte.Should().Be(validade);
    }

    // ─── CA-03b: contraprova — token sem validade é aceito ─────────────────

    [Fact]
    public void EffectiveGrant_TokenSemValidade_Aceita()
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, FonteGrant.Token);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ValidoAte.Should().BeNull();
    }

    // ─── ADR-0084: concessão excepcional exige ao menos um escopo ──────────

    [Fact]
    public void EffectiveGrant_ExcepcionalSemEscopo_Rejeita()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado =
            EffectiveGrant.From(Permissao, FonteGrant.PermissaoExcecional, validoAte: validade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoExcecionalObrigatorio,
            "uma concessão fora do perfil padrão nunca é global irrestrita (ADR-0084)");
    }

    [Fact]
    public void EffectiveGrant_ExcepcionalComRecursoTipoEmBranco_Rejeita()
    {
        // Restrição de recurso só com espaços não conta como escopo.
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, recursoTipoRestricao: "   ", validoAte: validade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoExcecionalObrigatorio);
    }

    [Theory]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    [InlineData("recurso")]
    public void EffectiveGrant_ExcepcionalComUmEscopo_Aceita(string escopo)
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.PermissaoExcecional,
            escopoUnidadeId: escopo == "unidade" ? Guid.CreateVersion7() : null,
            escopoProcessoId: escopo == "processo" ? Guid.CreateVersion7() : null,
            escopoChamadaId: escopo == "chamada" ? Guid.CreateVersion7() : null,
            recursoTipoRestricao: escopo == "recurso" ? "Edital" : null,
            validoAte: validade);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Fonte.Should().Be(FonteGrant.PermissaoExcecional);
    }

    [Fact]
    public void EffectiveGrant_ExcepcionalComEscopoGuidVazio_Rejeita()
    {
        // Guid.Empty não é um identificador real: a concessão excepcional não
        // pode atravessar o boundary com escopo "informado mas vazio".
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, escopoUnidadeId: Guid.Empty, validoAte: validade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
    }

    [Theory]
    [InlineData("unidade")]
    [InlineData("processo")]
    [InlineData("chamada")]
    public void EffectiveGrant_EscopoGuidVazio_Rejeita(string escopo)
    {
        // Vale para qualquer fonte — um escopo Guid.Empty é sempre inválido.
        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.Token,
            escopoUnidadeId: escopo == "unidade" ? Guid.Empty : null,
            escopoProcessoId: escopo == "processo" ? Guid.Empty : null,
            escopoChamadaId: escopo == "chamada" ? Guid.Empty : null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoInvalido);
    }

    // ─── Permissão obrigatória ─────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EffectiveGrant_PermissaoVazia_Rejeita(string? permissao)
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(permissao, FonteGrant.Token);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
    }
}
