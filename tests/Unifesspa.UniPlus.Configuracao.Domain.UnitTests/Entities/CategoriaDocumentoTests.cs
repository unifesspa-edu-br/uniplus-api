namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CategoriaDocumentoTests
{
    private const string Codigo = "DOCUMENTO_PROCESSUAL";
    private const string Nome = "Documento processual";

    [Fact(DisplayName = "Criar com campos válidos normaliza e preenche a categoria")]
    public void Criar_CamposValidos_Preenche()
    {
        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar(
            $"  {Codigo}  ", $"  {Nome}  ", "  Instrui o processo administrativo  ", 30);

        resultado.IsSuccess.Should().BeTrue();
        CategoriaDocumento categoria = resultado.Value!;
        categoria.Codigo.Valor.Should().Be(Codigo);
        categoria.Nome.Should().Be(Nome);
        categoria.Descricao.Should().Be("Instrui o processo administrativo");
        categoria.Ordem.Should().Be(30);
        categoria.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Descrição em branco vira nula")]
    public void Criar_DescricaoEmBranco_ViraNula()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(Codigo, Nome, "   ", 0).Value!;

        categoria.Descricao.Should().BeNull();
    }

    [Fact(DisplayName = "Ordem ausente equivale a zero")]
    public void Criar_SemOrdem_UsaZero()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(Codigo, Nome, null, null).Value!;

        categoria.Ordem.Should().Be(0);
    }

    [Fact(DisplayName = "Ordem negativa é recusada com OrdemInvalida no campo ordem")]
    public void Criar_OrdemNegativa_Recusa()
    {
        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar(Codigo, Nome, null, -1);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("ordem");
        resultado.Errors[0].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.OrdemInvalida);
    }

    [Fact(DisplayName = "Nome em branco é recusado com NomeObrigatorio")]
    public void Criar_NomeEmBranco_Recusa()
    {
        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar(Codigo, "  ", null, 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.NomeObrigatorio);
    }

    [Theory(DisplayName = "Nome fora da faixa de tamanho é recusado com NomeTamanho")]
    [InlineData("A")]
    [InlineData("nome longo demais")]
    public void Criar_NomeForaDaFaixa_Recusa(string nome)
    {
        string valor = nome == "nome longo demais" ? new string('N', 201) : nome;

        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar(Codigo, valor, null, 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.NomeTamanho);
    }

    [Fact(DisplayName = "Descrição acima de 1000 caracteres é recusada com DescricaoTamanho")]
    public void Criar_DescricaoLonga_Recusa()
    {
        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar(Codigo, Nome, new string('D', 1001), 0);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Violações independentes acumulam num único lote, na ordem dos campos")]
    public void Criar_VariasViolacoes_Acumula()
    {
        Result<CategoriaDocumento> resultado = CategoriaDocumento.Criar("01", "", null, -5);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Field).Should().Equal(["codigo", "nome", "ordem"]);
        resultado.Errors[0].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoFormatoInvalido);
        resultado.Errors[1].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Error.Code.Should().Be(CategoriaDocumentoErrorCodes.OrdemInvalida);
    }

    [Fact(DisplayName = "Atualizar troca código, nome, descrição e ordem")]
    public void Atualizar_CamposValidos_Aplica()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(Codigo, Nome, "antiga", 10).Value!;

        Result resultado = categoria.Atualizar("PRODUCAO_AVALIATIVA", "Produção avaliativa", "nova", 20);

        resultado.IsSuccess.Should().BeTrue();
        categoria.Codigo.Valor.Should().Be("PRODUCAO_AVALIATIVA");
        categoria.Nome.Should().Be("Produção avaliativa");
        categoria.Descricao.Should().Be("nova");
        categoria.Ordem.Should().Be(20);
    }

    [Fact(DisplayName = "Atualizar sem ordem informada reposiciona a categoria em zero")]
    public void Atualizar_SemOrdem_VoltaParaZero()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(Codigo, Nome, null, 10).Value!;

        Result resultado = categoria.Atualizar(Codigo, Nome, null, null);

        resultado.IsSuccess.Should().BeTrue();
        categoria.Ordem.Should().Be(0, "a atualização substitui o recurso por inteiro — ordem ausente é ordem zero");
    }

    [Fact(DisplayName = "Atualizar com campos inválidos não muta a categoria")]
    public void Atualizar_Invalido_NaoMuta()
    {
        CategoriaDocumento categoria = CategoriaDocumento.Criar(Codigo, Nome, null, 10).Value!;

        Result resultado = categoria.Atualizar("minusculo", "", null, -1);

        resultado.IsFailure.Should().BeTrue();
        categoria.Codigo.Valor.Should().Be(Codigo);
        categoria.Nome.Should().Be(Nome);
        categoria.Ordem.Should().Be(10);
    }
}
