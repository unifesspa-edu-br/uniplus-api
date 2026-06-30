namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoDocumentoTests
{
    private const string Codigo = "LAUDO_MEDICO";
    private const string Nome = "Laudo médico";
    private const string Categoria = "SAUDE";

    private static Result<TipoDocumento> Criar(
        string codigo = Codigo,
        string nome = Nome,
        string? descricao = null,
        string categoria = Categoria,
        string? formatosAceitos = "pdf,jpg",
        int? tamanhoMaximoMb = 10,
        string? tipoEquivalente = null) =>
        TipoDocumento.Criar(codigo, nome, descricao, categoria, formatosAceitos, tamanhoMaximoMb, tipoEquivalente);

    [Fact(DisplayName = "Criar com dados válidos preenche os campos e fica ativo com Guid v7")]
    public void Criar_DadosValidos_Preenche()
    {
        TipoDocumento tipo = Criar(descricao: "Laudo emitido por profissional de saúde").Value!;

        tipo.Id.Should().NotBe(Guid.Empty);
        tipo.Codigo.Should().Be(Codigo);
        tipo.Nome.Should().Be(Nome);
        tipo.Descricao.Should().Be("Laudo emitido por profissional de saúde");
        tipo.Categoria.Should().Be(CategoriaDocumento.Saude);
        tipo.FormatosAceitos.Should().Be("pdf,jpg");
        tipo.TamanhoMaximoMb.Should().Be(10);
        tipo.TipoEquivalente.Should().BeNull();
        tipo.IsDeleted.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar sem campos opcionais (descrição, formatos, tamanho, equivalente) é aceito")]
    public void Criar_SemOpcionais_Aceita()
    {
        TipoDocumento tipo = Criar(descricao: null, formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null).Value!;

        tipo.Descricao.Should().BeNull();
        tipo.FormatosAceitos.Should().BeNull();
        tipo.TamanhoMaximoMb.Should().BeNull();
        tipo.TipoEquivalente.Should().BeNull();
    }

    [Theory(DisplayName = "Código ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCodigo_Falha(string codigo)
    {
        Result<TipoDocumento> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "Código acima do tamanho máximo é rejeitado")]
    public void Criar_CodigoLongo_Falha()
    {
        Result<TipoDocumento> resultado = Criar(codigo: new string('A', 61));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CodigoTamanho);
    }

    [Theory(DisplayName = "Nome ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemNome_Falha(string nome)
    {
        Result<TipoDocumento> resultado = Criar(nome: nome);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NomeObrigatorio);
    }

    [Theory(DisplayName = "Categoria fora do domínio fechado é rejeitada (incl. numérico e PascalCase)")]
    [InlineData("FINANCEIRO")]
    [InlineData("1")]
    [InlineData("Saude")]
    [InlineData("")]
    public void Criar_CategoriaInvalida_Falha(string categoria)
    {
        Result<TipoDocumento> resultado = Criar(categoria: categoria);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaInvalida);
    }

    [Theory(DisplayName = "Tamanho máximo zero ou negativo é rejeitado; positivo e nulo são aceitos")]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(10, true)]
    [InlineData(null, true)]
    public void Criar_TamanhoMaximo_ValidaPositividade(int? mb, bool deveAceitar)
    {
        Result<TipoDocumento> resultado = Criar(tamanhoMaximoMb: mb);

        resultado.IsSuccess.Should().Be(deveAceitar);
        if (!deveAceitar)
        {
            resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.TamanhoMaximoInvalido);
        }
    }

    [Fact(DisplayName = "Tipo equivalente igual ao próprio código é rejeitado")]
    public void Criar_EquivalenteIgualCodigo_Falha()
    {
        Result<TipoDocumento> resultado = Criar(codigo: "RG", tipoEquivalente: "RG");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo);
    }

    [Fact(DisplayName = "Tipo equivalente apontando para outro código é aceito como rótulo classificatório")]
    public void Criar_EquivalenteOutroCodigo_Aceita()
    {
        TipoDocumento tipo = Criar(codigo: "RG", categoria: "IDENTIFICACAO", tipoEquivalente: "CIN").Value!;

        tipo.TipoEquivalente.Should().Be("CIN");
    }

    [Fact(DisplayName = "Atualizar troca os atributos editáveis, inclusive o código (editável)")]
    public void Atualizar_AlteraAtributos_InclusiveCodigo()
    {
        TipoDocumento tipo = Criar(codigo: "CIN", categoria: "IDENTIFICACAO").Value!;
        Guid idOriginal = tipo.Id;

        Result resultado = tipo.Atualizar(
            "CIN_NOVO", "Carteira de Identidade Nacional", "Documento unificado", "IDENTIFICACAO", "pdf", 5, null);

        resultado.IsSuccess.Should().BeTrue();
        tipo.Codigo.Should().Be("CIN_NOVO");
        tipo.Nome.Should().Be("Carteira de Identidade Nacional");
        tipo.Categoria.Should().Be(CategoriaDocumento.Identificacao);
        tipo.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o código editável");
    }
}
