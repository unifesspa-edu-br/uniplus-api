namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoDeficienciaTests
{
    private const string Nome = "Visual";

    private static Result<TipoDeficiencia> Criar(
        string nome = Nome,
        string? descricao = null) =>
        TipoDeficiencia.Criar(nome, descricao);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativo com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        TipoDeficiencia tipo = Criar(descricao: "Deficiência relacionada à visão").Value!;

        tipo.Id.Should().NotBe(Guid.Empty);
        tipo.Nome.Should().Be(Nome);
        tipo.Descricao.Should().Be("Deficiência relacionada à visão");
        tipo.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar sem descrição (opcional) é aceito")]
    public void Criar_SemDescricao_Aceita()
    {
        TipoDeficiencia tipo = Criar(descricao: null).Value!;

        tipo.Descricao.Should().BeNull();
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<TipoDeficiencia> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome abaixo do tamanho mínimo é rejeitado")]
    public void Criar_NomeCurto_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(nome: "A");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(nome: new string('A', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Descrição acima do tamanho máximo é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<TipoDeficiencia> resultado = Criar(descricao: new string('A', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis e preserva o Id imutável")]
    public void Atualizar_AlteraAtributos_PreservaId()
    {
        TipoDeficiencia tipo = Criar(nome: "Visual").Value!;
        Guid idOriginal = tipo.Id;

        Result resultado = tipo.Atualizar("Auditiva", "Deficiência relacionada à audição");

        resultado.IsSuccess.Should().BeTrue();
        tipo.Nome.Should().Be("Auditiva");
        tipo.Descricao.Should().Be("Deficiência relacionada à audição");
        tipo.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o nome editável");
    }

    [Fact(DisplayName = "Atualizar com nome inválido falha e não altera o estado")]
    public void Atualizar_NomeInvalido_Falha()
    {
        TipoDeficiencia tipo = Criar(nome: "Visual").Value!;

        Result resultado = tipo.Atualizar("A", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDeficienciaErrorCodes.NomeTamanho);
        tipo.Nome.Should().Be("Visual");
    }
}
