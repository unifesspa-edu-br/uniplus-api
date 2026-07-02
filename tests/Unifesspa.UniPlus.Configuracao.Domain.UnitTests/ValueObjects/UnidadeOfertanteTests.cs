namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class UnidadeOfertanteTests
{
    private static readonly Guid OrigemId = Guid.CreateVersion7();

    private static Result<UnidadeOfertante> Criar(
        Guid? origemId = null,
        string? sigla = "FACET",
        string? nome = "Faculdade de Computação e Engenharia Elétrica",
        string? tipo = "Faculdade") =>
        UnidadeOfertante.Criar(origemId ?? OrigemId, sigla, nome, tipo);

    [Fact(DisplayName = "Criar com dados válidos congela origem, sigla, nome e tipo")]
    public void Criar_DadosValidos_Preenche()
    {
        UnidadeOfertante unidade = Criar().Value!;

        unidade.OrigemId.Should().Be(OrigemId);
        unidade.Sigla.Should().Be("FACET");
        unidade.Nome.Should().Be("Faculdade de Computação e Engenharia Elétrica");
        unidade.Tipo.Should().Be("Faculdade");
    }

    [Fact(DisplayName = "Campos são normalizados por Trim na criação")]
    public void Criar_ComEspacos_Normaliza()
    {
        UnidadeOfertante unidade = Criar(
            sigla: "  FACET  ", nome: "  Faculdade de Computação  ", tipo: "  Faculdade  ").Value!;

        unidade.Sigla.Should().Be("FACET");
        unidade.Nome.Should().Be("Faculdade de Computação");
        unidade.Tipo.Should().Be("Faculdade");
    }

    [Fact(DisplayName = "OrigemId vazio é rejeitado — sem proveniência não há snapshot")]
    public void Criar_OrigemVazia_Falha()
    {
        Result<UnidadeOfertante> resultado = Criar(origemId: Guid.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteOrigemObrigatoria);
    }

    [Theory(DisplayName = "Sigla ausente ou em branco é rejeitada")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemSigla_Falha(string? sigla)
    {
        Result<UnidadeOfertante> resultado = Criar(sigla: sigla);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteSiglaObrigatoria);
    }

    [Fact(DisplayName = "Sigla acima do tamanho máximo é rejeitada")]
    public void Criar_SiglaLonga_Falha()
    {
        Result<UnidadeOfertante> resultado = Criar(sigla: new string('S', 51));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteSiglaTamanho);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string? nome)
    {
        Result<UnidadeOfertante> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteNomeObrigatorio);
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<UnidadeOfertante> resultado = Criar(nome: new string('N', 251));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteNomeTamanho);
    }

    [Theory(DisplayName = "Tipo ausente ou em branco é rejeitado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemTipo_Falha(string? tipo)
    {
        Result<UnidadeOfertante> resultado = Criar(tipo: tipo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteTipoObrigatorio);
    }

    [Fact(DisplayName = "Tipo acima do tamanho máximo é rejeitado")]
    public void Criar_TipoLongo_Falha()
    {
        Result<UnidadeOfertante> resultado = Criar(tipo: new string('T', 31));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(OfertaCursoErrorCodes.UnidadeOfertanteTipoTamanho);
    }

    [Fact(DisplayName = "Igualdade estrutural de record: mesmos campos, mesmo valor")]
    public void Igualdade_Estrutural()
    {
        Guid origem = Guid.CreateVersion7();

        UnidadeOfertante a = UnidadeOfertante.Criar(origem, "FACET", "Faculdade de Computação", "Faculdade").Value!;
        UnidadeOfertante b = UnidadeOfertante.Criar(origem, "FACET", "Faculdade de Computação", "Faculdade").Value!;

        a.Should().Be(b);
    }
}
