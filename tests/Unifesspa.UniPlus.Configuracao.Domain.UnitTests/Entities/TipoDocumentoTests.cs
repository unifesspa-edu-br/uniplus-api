namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
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
        tipo.Categoria.Should().Be("SAUDE");
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

    [Theory(DisplayName = "Categoria fora do formato de código é rejeitada (numérico, PascalCase, acento)")]
    [InlineData("1")]
    [InlineData("Saude")]
    [InlineData("SAÚDE")]
    [InlineData("SAUDE-GERAL")]
    public void Criar_CategoriaForaDoFormato_Falha(string categoria)
    {
        Result<TipoDocumento> resultado = Criar(categoria: categoria);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaFormatoInvalido);
    }

    [Theory(DisplayName = "Categoria em branco é rejeitada como obrigatória, não como formato")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_CategoriaEmBranco_Falha(string categoria)
    {
        Result<TipoDocumento> resultado = Criar(categoria: categoria);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaObrigatoria);
    }

    [Fact(DisplayName = "Categoria bem formada fora do catálogo é aceita pelo agregado — quem confere existência é o handler")]
    public void Criar_CategoriaBemFormadaForaDoCatalogo_Aceita()
    {
        Result<TipoDocumento> resultado = Criar(categoria: "DOCUMENTO_MILITAR");

        resultado.IsSuccess.Should().BeTrue(
            "o agregado não conhece mais o roster: validar existência exigiria I/O, que o domínio não faz");
        resultado.Value!.Categoria.Should().Be("DOCUMENTO_MILITAR");
    }

    [Fact(DisplayName = "Categoria é normalizada antes de persistir, não só na comparação")]
    public void Criar_CategoriaComEspacoEmVolta_PersisteNormalizada()
    {
        // A comparação com o cadastro é ordinal: o valor que persiste tem de ser o
        // normalizado, senão " RENDA " nunca casaria com a categoria RENDA viva.
        // A normalização Unicode acompanha o Trim, mas é inócua enquanto o formato
        // aceita só ASCII — nenhuma letra de A a Z tem forma decomposta.
        Result<TipoDocumento> resultado = Criar(categoria: "  RENDA  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Categoria.Should().Be("RENDA");
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

    /// <summary>
    /// O guard de auto-equivalência compara os valores já normalizados dos dois
    /// campos — só roda quando ambos passaram nas próprias checagens individuais.
    /// Com código ausente não há valor normalizado para comparar, então o guard
    /// nunca dispara, mesmo com um TipoEquivalente presente.
    /// </summary>
    [Fact(DisplayName = "Código ausente não dispara o guard de auto-equivalência")]
    public void Criar_CodigoAusenteComTipoEquivalentePresente_NaoDisparaAutoEquivalencia()
    {
        Result<TipoDocumento> resultado = TipoDocumento.Criar(
            "", Nome, null, Categoria, null, null, "RG");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Error.Code.Should().Be(TipoDocumentoErrorCodes.CodigoObrigatorio);
    }

    // ── Acumulação (ADR-0125) ──────────────────────────────────────────────────

    /// <summary>
    /// Antes: ValidarCampos retornava no primeiro campo inválido, mascarando as
    /// demais violações — paridade com o FluentValidation removido, que
    /// reportava cada campo em separado.
    /// </summary>
    [Fact(DisplayName = "Código, nome, categoria e tamanho máximo inválidos ao mesmo tempo acumulam as quatro violações rotuladas")]
    public void Criar_QuatroCamposInvalidos_AcumulaAsQuatroViolacoesRotuladas()
    {
        Result<TipoDocumento> resultado = TipoDocumento.Criar(
            "", "", null, "1", null, 0, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(4);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoDocumentoErrorCodes.CodigoObrigatorio);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(TipoDocumentoErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("categoria");
        resultado.Errors[2].Error.Code.Should().Be(TipoDocumentoErrorCodes.CategoriaFormatoInvalido);
        resultado.Errors[3].Field.Should().Be("tamanhoMaximoMb");
        resultado.Errors[3].Error.Code.Should().Be(TipoDocumentoErrorCodes.TamanhoMaximoInvalido);
    }

    [Fact(DisplayName = "Tipo equivalente igual ao código válido acumula junto com nome ausente")]
    public void Criar_NomeAusenteEEquivalenteIgualCodigo_AcumulaAsDuasViolacoes()
    {
        Result<TipoDocumento> resultado = TipoDocumento.Criar(
            "RG", "", null, Categoria, null, null, "RG");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("nome");
        resultado.Errors[1].Field.Should().Be("tipoEquivalente");
        resultado.Errors[1].Error.Code.Should().Be(TipoDocumentoErrorCodes.TipoEquivalenteIgualCodigo);
    }

    [Fact(DisplayName = "Campos obrigatórios nulos não lançam — devolvem a violação de domínio")]
    public void Criar_CamposObrigatoriosNulos_NaoLancaEDevolveViolacaoDeDominio()
    {
        Result<TipoDocumento> resultado = TipoDocumento.Criar(
            null, null, null, null, null, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
    }

    /// <summary>
    /// O limite vale sobre o valor já normalizado (Trim), não sobre o bruto —
    /// FluentValidation media o bruto; essa era uma divergência real entre as duas
    /// camadas que o domínio como fonte única elimina.
    /// </summary>
    [Fact(DisplayName = "Descrição com 1000 caracteres úteis mais espaços nas pontas é aceita")]
    public void Criar_DescricaoNoLimiteComEspacosNasPontas_Aceita()
    {
        TipoDocumento tipo = Criar(descricao: "  " + new string('a', 1000) + "  ").Value!;

        tipo.Descricao.Should().Be(new string('a', 1000));
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
        tipo.Categoria.Should().Be("IDENTIFICACAO");
        tipo.Id.Should().Be(idOriginal, "o Id é imutável mesmo com o código editável");
    }
}
