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

        Result<EffectiveGrant> concessao = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoInstitucional: true);
        Result<EffectiveGrant> oidc = EffectiveGrant.From(
            Permissao, FonteGrant.OidcGroupBinding, escopoInstitucional: true, validoAte: validade);
        Result<EffectiveGrant> excepcional = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, escopoUnidadeId: Guid.CreateVersion7(), validoAte: validade);

        concessao.Value!.Fonte.Should().Be(FonteGrant.Concessao);
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
        Guid perfil = Guid.CreateVersion7();

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.Concessao,
            grantId: grantId,
            escopoUnidadeId: unidade,
            escopoProcessoId: processo,
            escopoChamadaId: chamada,
            recursoTipoRestricao: "Edital",
            perfilId: perfil);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.GrantId.Should().Be(grantId);
        resultado.Value.EscopoUnidadeId.Should().Be(unidade);
        resultado.Value.EscopoProcessoId.Should().Be(processo);
        resultado.Value.EscopoChamadaId.Should().Be(chamada);
        resultado.Value.RecursoTipoRestricao.Should().Be("Edital");
        resultado.Value.PerfilId.Should().Be(perfil);
        resultado.Value.EscopoInstitucional.Should().BeFalse();
    }

    // ─── CA-03b: concessão server-side sem validade é rejeitada ────────────

    [Theory]
    [InlineData(FonteGrant.OidcGroupBinding)]
    [InlineData(FonteGrant.PermissaoExcecional)]
    public void EffectiveGrant_ServerSideSemValidade_Rejeita(FonteGrant fonte)
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, fonte, escopoInstitucional: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantValidadeObrigatoria);
    }

    [Fact]
    public void EffectiveGrant_OidcGroupBindingComValidadeEEscopoInstitucional_Aceita()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(2);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.OidcGroupBinding, escopoInstitucional: true, validoAte: validade);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ValidoAte.Should().Be(validade);
    }

    // ─── A concessão de perfil não precisa de validade ─────────────────────

    [Fact]
    public void EffectiveGrant_ConcessaoSemValidade_Aceita()
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, FonteGrant.Concessao, escopoInstitucional: true);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.ValidoAte.Should().BeNull();
    }

    // ─── Toda concessão tem a unidade ou a instituição como escopo ─────────

    [Fact]
    public void EffectiveGrant_ConcessaoSemUnidadeNemInstituicao_Rejeita()
    {
        // Só processo, chamada ou tipo de recurso não são escopo de concessão de perfil: seria
        // permissão global em todas as unidades.
        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.Concessao,
            escopoProcessoId: Guid.CreateVersion7(),
            recursoTipoRestricao: "Edital");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoObrigatorio);
    }

    // ─── ADR-0084: concessão excepcional exige ao menos um escopo ──────────

    [Fact]
    public void EffectiveGrant_ExcepcionalSemEscopo_Rejeita()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, escopoInstitucional: true, validoAte: validade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoExcepcionalObrigatorio,
            "só o escopo institucional não basta para a concessão fora do perfil padrão (ADR-0084)");
    }

    [Fact]
    public void EffectiveGrant_ExcepcionalComRecursoTipoEmBranco_Rejeita()
    {
        DateTimeOffset validade = DateTimeOffset.UtcNow.AddHours(1);

        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.PermissaoExcecional, recursoTipoRestricao: "   ", validoAte: validade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoExcepcionalObrigatorio);
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
    }

    [Fact]
    public void EffectiveGrant_UnidadeEInstituicaoJuntas_Rejeita()
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao, FonteGrant.Concessao, escopoUnidadeId: Guid.CreateVersion7(), escopoInstitucional: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantEscopoAmbiguo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EffectiveGrant_UnidadeOuInstituicao_Aceita(bool institucional)
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(
            Permissao,
            FonteGrant.Concessao,
            escopoUnidadeId: institucional ? null : Guid.CreateVersion7(),
            escopoInstitucional: institucional);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.EscopoInstitucional.Should().Be(institucional);
        (resultado.Value.EscopoUnidadeId is null).Should().Be(institucional);
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

    [Fact]
    public void EffectiveGrant_GrantIdGuidVazio_Rejeita()
    {
        Result<EffectiveGrant> resultado = EffectiveGrant.From(Permissao, FonteGrant.Concessao, grantId: Guid.Empty, escopoInstitucional: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantGrantIdInvalido);
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
            FonteGrant.Concessao,
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
        Result<EffectiveGrant> resultado = EffectiveGrant.From(permissao, FonteGrant.Concessao, escopoInstitucional: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AuthorizationErrorCodes.EffectiveGrantPermissaoObrigatoria);
    }
}
