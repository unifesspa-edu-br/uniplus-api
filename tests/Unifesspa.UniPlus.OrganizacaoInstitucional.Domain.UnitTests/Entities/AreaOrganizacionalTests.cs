namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

public sealed class AreaOrganizacionalTests
{
    private static AreaCodigo CodigoValido => AreaCodigo.From("CEPS").Value;

    [Fact(DisplayName = "Criar com dados válidos retorna Success com agregado preenchido")]
    public void Criar_ComDadosValidos_DeveRetornarSuccess()
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido,
            "Centro de Processos Seletivos",
            TipoAreaOrganizacional.Centro,
            "Unidade responsável pelos processos seletivos da Unifesspa.",
            "0055-organizacao-institucional-bounded-context");

        resultado.IsSuccess.Should().BeTrue();
        AreaOrganizacional area = resultado.Value!;
        area.Codigo.Should().Be(CodigoValido);
        area.Nome.Should().Be("Centro de Processos Seletivos");
        area.Tipo.Should().Be(TipoAreaOrganizacional.Centro);
        area.Descricao.Should().Be("Unidade responsável pelos processos seletivos da Unifesspa.");
        area.AdrReferenceCode.Should().Be("0055-organizacao-institucional-bounded-context");
        area.Id.Should().NotBeEmpty("ADR-0032 exige Guid v7 via EntityBase");
        area.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar com nome vazio/whitespace retorna NomeObrigatorio")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_DeveRetornarNomeObrigatorio(string nomeInvalido)
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, nomeInvalido, TipoAreaOrganizacional.Centro,
            "Descricao válida.", "0055-organizacao-institucional-bounded-context");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Criar com nome de 1 char retorna NomeTamanho (limite inferior)")]
    public void Criar_ComNomeMuitoCurto_DeveRetornarNomeTamanho()
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, "a", TipoAreaOrganizacional.Centro,
            "Descricao válida.", "0055-organizacao-institucional-bounded-context");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Criar com nome > 120 chars retorna NomeTamanho (limite superior)")]
    public void Criar_ComNomeMuitoLongo_DeveRetornarNomeTamanho()
    {
        string nomeInvalido = new('a', 121);

        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, nomeInvalido, TipoAreaOrganizacional.Centro,
            "Descricao válida.", "0055-organizacao-institucional-bounded-context");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Criar com descrição vazia retorna DescricaoObrigatoria")]
    public void Criar_ComDescricaoVazia_DeveRetornarDescricaoObrigatoria()
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, "Nome valido", TipoAreaOrganizacional.Centro,
            "  ", "0055-organizacao-institucional-bounded-context");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com descrição > 500 chars retorna DescricaoTamanho")]
    public void Criar_ComDescricaoMuitoLonga_DeveRetornarDescricaoTamanho()
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, "Nome valido", TipoAreaOrganizacional.Centro,
            new string('x', 501),
            "0055-organizacao-institucional-bounded-context");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.DescricaoTamanho);
    }

    [Theory(DisplayName = "Criar com AdrReferenceCode vazio retorna AdrReferenceObrigatorio (BDD-3)")]
    [InlineData("")]
    [InlineData("    ")]
    public void Criar_ComAdrReferenceVazio_DeveRetornarAdrReferenceObrigatorio(string adrInvalido)
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, "Nome valido", TipoAreaOrganizacional.Centro,
            "Descricao válida.", adrInvalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.AdrReferenceObrigatorio);
    }

    [Theory(DisplayName = "Criar com AdrReferenceCode em formato inválido retorna AdrReferenceFormatoInvalido")]
    [InlineData("055-foo")]              // 3 dígitos
    [InlineData("0055")]                 // sem slug
    [InlineData("0055_foo")]             // underscore em vez de hífen
    [InlineData("0055-MAIUSCULAS")]      // letras maiúsculas
    [InlineData("abcd-foo")]             // sem dígitos no prefixo
    [InlineData("0055-")]                // trailing hyphen sem slug
    [InlineData("0055--foo")]            // double hyphen
    [InlineData("0055-foo-")]            // trailing hyphen
    [InlineData("0055-foo--bar")]        // double hyphen no meio
    public void Criar_ComAdrReferenceFormaInvalido_DeveRetornarAdrReferenceFormatoInvalido(string adrInvalido)
    {
        Result<AreaOrganizacional> resultado = AreaOrganizacional.Criar(
            CodigoValido, "Nome valido", TipoAreaOrganizacional.Centro,
            "Descricao válida.", adrInvalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.AdrReferenceFormatoInvalido);
    }

    [Fact(DisplayName = "Atualizar com dados válidos altera Nome, Tipo, Descricao")]
    public void Atualizar_ComDadosValidos_DeveAlterarCampos()
    {
        AreaOrganizacional area = AreaOrganizacional.Criar(
            CodigoValido, "Nome inicial", TipoAreaOrganizacional.Centro,
            "Desc inicial.", "0055-organizacao-institucional-bounded-context").Value!;

        Result resultado = area.Atualizar("Nome alterado", TipoAreaOrganizacional.Plataforma, "Nova descricao.");

        resultado.IsSuccess.Should().BeTrue();
        area.Nome.Should().Be("Nome alterado");
        area.Tipo.Should().Be(TipoAreaOrganizacional.Plataforma);
        area.Descricao.Should().Be("Nova descricao.");
        area.Codigo.Should().Be(CodigoValido, "Codigo é imutável após criação (ADR-0057 §Invariante 2)");
    }

    [Fact(DisplayName = "Atualizar com nome vazio retorna NomeObrigatorio")]
    public void Atualizar_ComNomeVazio_DeveRetornarNomeObrigatorio()
    {
        AreaOrganizacional area = AreaOrganizacional.Criar(
            CodigoValido, "Nome inicial", TipoAreaOrganizacional.Centro,
            "Desc inicial.", "0055-organizacao-institucional-bounded-context").Value!;

        Result resultado = area.Atualizar(string.Empty, TipoAreaOrganizacional.Centro, "Desc.");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AreaOrganizacionalErrorCodes.NomeObrigatorio);
    }
}
