namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RecursoAcessibilidadeTests
{
    private const string Codigo = "LEDOR";
    private const string Nome = "Ledor";

    private static Result<RecursoAcessibilidade> Criar(
        string? codigo = Codigo,
        string nome = Nome,
        string? descricao = null) =>
        RecursoAcessibilidade.Criar(codigo, nome, descricao);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativo com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        RecursoAcessibilidade recurso = Criar(descricao: "Profissional que lê a prova ao candidato").Value!;

        recurso.Id.Should().NotBe(Guid.Empty);
        recurso.Codigo.Valor.Should().Be(Codigo);
        recurso.Nome.Should().Be(Nome);
        recurso.Descricao.Should().Be("Profissional que lê a prova ao candidato");
        recurso.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar sem descrição é aceito e mantém o campo nulo")]
    public void Criar_SemDescricao_Aceita()
    {
        RecursoAcessibilidade recurso = Criar(descricao: null).Value!;

        recurso.Descricao.Should().BeNull();
    }

    [Theory(DisplayName = "Código ausente ou fora do formato é rejeitado no campo codigo")]
    [InlineData(null, RecursoAcessibilidadeErrorCodes.CodigoObrigatorio)]
    [InlineData("ledor", RecursoAcessibilidadeErrorCodes.CodigoFormatoInvalido)]
    public void Criar_CodigoInvalido_Falha(string? codigo, string erroEsperado)
    {
        Result<RecursoAcessibilidade> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("codigo");
        resultado.Error!.Code.Should().Be(erroEsperado);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<RecursoAcessibilidade> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Nome abaixo do tamanho mínimo (2) é rejeitado")]
    public void Criar_NomeCurto_Falha()
    {
        Result<RecursoAcessibilidade> resultado = Criar(nome: "A");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Nome acima do tamanho máximo (200) é rejeitado")]
    public void Criar_NomeLongo_Falha()
    {
        Result<RecursoAcessibilidade> resultado = Criar(nome: new string('A', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Descrição acima do tamanho máximo (1000) é rejeitada")]
    public void Criar_DescricaoLonga_Falha()
    {
        Result<RecursoAcessibilidade> resultado = Criar(descricao: new string('D', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis, inclusive código e nome, preservando o Id")]
    public void Atualizar_AlteraAtributos_PreservaId()
    {
        RecursoAcessibilidade recurso = Criar(nome: "Ledor").Value!;
        Guid idOriginal = recurso.Id;

        Result resultado = recurso.Atualizar("TEMPO_ADICIONAL", "Tempo adicional", "Acréscimo de tempo para realização da prova");

        resultado.IsSuccess.Should().BeTrue();
        recurso.Codigo.Valor.Should().Be("TEMPO_ADICIONAL");
        recurso.Nome.Should().Be("Tempo adicional");
        recurso.Descricao.Should().Be("Acréscimo de tempo para realização da prova");
        recurso.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o nome editável");
    }

    [Fact(DisplayName = "Código, nome e descrição inválidos acumulam as três violações")]
    public void Criar_CodigoNomeEDescricaoInvalidos_AcumulaAsTresViolacoes()
    {
        Result<RecursoAcessibilidade> resultado = Criar(codigo: "", nome: "", descricao: new string('D', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(static e => e.Field).Should().Equal("codigo", "nome", "descricao");
    }
}
