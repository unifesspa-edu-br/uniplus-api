namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using Xunit;

public sealed class BaseLegalDoFadaTests
{
    private static readonly (string CodigoIbge, string Nome, string Uf) MunicipioValido = ("1504208", "Marabá", "PA");

    [Fact(DisplayName = "Criar com dados válidos retorna sucesso com os campos normalizados")]
    public void Criar_ComDadosValidos_RetornaSucesso()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI",
            "Lei Estadual 123",
            "Dispõe sobre o FADA",
            [MunicipioValido]);

        result.IsSuccess.Should().BeTrue();
        BaseLegalDoFada entity = result.Value!;
        entity.TipoInstrumento.Should().Be(TipoInstrumentoNormativo.Lei);
        entity.Identificacao.Should().Be("Lei Estadual 123");
        entity.Descricao.Should().Be("Dispõe sobre o FADA");
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Criar sem nenhum município retorna ValidationFailure com SemMunicipios")]
    public void Criar_SemMunicipio_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Lei 123", "Descricao", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.SemMunicipios);
    }

    [Fact(DisplayName = "Criar com código IBGE fora do formato de 7 dígitos retorna MunicipioInvalido")]
    public void Criar_ComCodigoIbgeInvalido_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Lei 123", "Descricao", [("123", "Marabá", "PA")]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.MunicipioInvalido);
    }

    [Fact(DisplayName = "Criar com tipo de instrumento fora do vocabulário fechado retorna TipoInstrumentoInvalido")]
    public void Criar_ComTipoInstrumentoInvalido_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "INVALIDO", "Lei 123", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido);
    }

    [Fact(DisplayName = "Criar com tipo de instrumento inválido reporta o erro no campo tipoInstrumento (nome da propriedade do contrato)")]
    public void Criar_ComTipoInstrumentoInvalido_ReportaErroNoCampoDoContrato()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "INVALIDO", "Lei 123", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e =>
            e.Error.Code == BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido && e.Field == "tipoInstrumento");
    }

    [Fact(DisplayName = "A recusa do tipo desconhecido lista os códigos aceitos sem repetir o valor recebido")]
    public void Criar_TipoDesconhecido_ListaOsCodigosAceitosSemRepetirOValor()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "MEDIDA_PROVISORIA", "Lei 123", "Descricao", [MunicipioValido]);

        string mensagem = result.Errors.Single(e => e.Field == "tipoInstrumento").Error.Message;
        mensagem.Should().Contain("LEI, DECRETO, PORTARIA, RESOLUCAO, INSTRUCAO_NORMATIVA, PARECER, EDITAL");
        mensagem.Should().NotContain("MEDIDA_PROVISORIA");
    }

    [Fact(DisplayName = "O tipo desconhecido é recusado junto com as demais recusas do pedido")]
    public void Criar_TipoDesconhecidoEIdentificacaoVazia_AcumulaAsRecusas()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar("MEDIDA_PROVISORIA", "", "Descricao", []);

        result.Errors.Select(e => e.Error.Code).Should().Contain(
        [
            BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido,
            BaseLegalDoFadaErrorCodes.IdentificacaoObrigatoria,
            BaseLegalDoFadaErrorCodes.SemMunicipios,
        ]);
    }

    [Theory(DisplayName = "Tipo de instrumento em outra caixa, vazio ou nulo é recusado")]
    [InlineData("lei")]
    [InlineData("")]
    [InlineData(null)]
    public void Criar_TipoForaDoCodigoDeclarado_Recusa(string? tipo)
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(tipo, "Lei 123", "Descricao", [MunicipioValido]);

        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido);
    }

    [Fact(DisplayName = "Criar com identificação vazia retorna IdentificacaoObrigatoria")]
    public void Criar_ComIdentificacaoVazia_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com identificação abaixo do tamanho mínimo retorna IdentificacaoTamanho")]
    public void Criar_ComIdentificacaoCurta_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "AB", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoTamanho);
    }

    [Fact(DisplayName = "Criar com identificação acima do tamanho máximo retorna IdentificacaoTamanho")]
    public void Criar_ComIdentificacaoLonga_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", new string('A', 501), "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoTamanho);
    }

    [Fact(DisplayName = "Criar com caractere nulo na identificação retorna IdentificacaoCaractereInvalido")]
    public void Criar_ComIdentificacaoContendoCaractereNulo_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Lei\0Estadual", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com quebra de linha na identificação retorna IdentificacaoCaractereInvalido")]
    public void Criar_ComIdentificacaoComQuebraDeLinha_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Lei\nEstadual", "Descricao", [MunicipioValido]);

        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com descrição de várias linhas e tabulação cria a base legal")]
    public void Criar_ComDescricaoDeVariasLinhas_Cria()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Identificacao", "Art. 1º\r\n\tArt. 2º", [MunicipioValido]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Descricao.Should().Be("Art. 1º\r\n\tArt. 2º");
    }

    [Fact(DisplayName = "Criar com controle na descrição retorna DescricaoCaractereInvalido")]
    public void Criar_ComDescricaoComControle_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Identificacao", "Descricao\u0007", [MunicipioValido]);

        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.DescricaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com descrição vazia retorna DescricaoObrigatoria")]
    public void Criar_ComDescricaoVazia_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Identificacao", "", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com descrição acima do tamanho máximo retorna DescricaoTamanho")]
    public void Criar_ComDescricaoLonga_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Identificacao", new string('A', 2001), [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Criar com caractere nulo na descrição retorna DescricaoCaractereInvalido")]
    public void Criar_ComDescricaoContendoCaractereNulo_RetornaValidationFailure()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "LEI", "Identificacao", "Descricao\0com nulo", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.DescricaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar acumula todas as violações do payload no mesmo Result")]
    public void Criar_ComMultiplasViolacoes_AcumulaTodas()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar(
            "INVALIDO", "", "", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.IdentificacaoObrigatoria);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.DescricaoObrigatoria);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.SemMunicipios);
    }

    [Fact(DisplayName = "Atualizar mantém as mesmas regras de domínio da criação")]
    public void Atualizar_MantemRegrasDeDominio()
    {
        BaseLegalDoFada entity = BaseLegalDoFada.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        Result result = entity.Atualizar("INVALIDO", "Ident", "Desc", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido);
    }

    [Fact(DisplayName = "Atualizar substitui integralmente a lista de municípios anterior")]
    public void Atualizar_SubstituiMunicipios()
    {
        BaseLegalDoFada entity = BaseLegalDoFada.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        (string CodigoIbge, string Nome, string Uf) novoMunicipio = ("1501402", "Belém", "PA");
        Result result = entity.Atualizar("LEI", "Identificacao", "Descricao", [novoMunicipio]);

        result.IsSuccess.Should().BeTrue();
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be("1501402");
    }

    [Fact(DisplayName = "Atualizar com payload inválido não altera o estado anterior da entidade")]
    public void Atualizar_ComPayloadInvalido_NaoAlteraEstadoAnterior()
    {
        BaseLegalDoFada entity = BaseLegalDoFada.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        Result result = entity.Atualizar("LEI", "Identificacao", "Descricao", []);

        result.IsFailure.Should().BeTrue();
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be(MunicipioValido.CodigoIbge);
    }

    [Fact(DisplayName = "Criar forma o código estável a partir do tipo e da identificação")]
    public void Criar_FormaOCodigo()
    {
        BaseLegalDoFada entity = BaseLegalDoFada.Criar(
            "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Descricao", [MunicipioValido]).Value!;

        entity.Codigo.Valor.Should().Be("PORTARIA_UNIFESSPA_NO_2514_2023");
    }

    [Fact(DisplayName = "Atualizar tipo e identificação mantém o código da criação")]
    public void Atualizar_MantemOCodigo()
    {
        BaseLegalDoFada entity = BaseLegalDoFada.Criar("LEI", "Lei 123", "Descricao", [MunicipioValido]).Value!;

        Result result = entity.Atualizar("DECRETO", "Decreto 999", "Outra descricao", [MunicipioValido]);

        result.IsSuccess.Should().BeTrue();
        entity.TipoInstrumento.Should().Be(TipoInstrumentoNormativo.Decreto);
        entity.Identificacao.Should().Be("Decreto 999");
        entity.Codigo.Valor.Should().Be("LEI_123");
    }

    [Fact(DisplayName = "Identificação válida cujo código passa do limite é recusada citando o limite, junto com as demais recusas")]
    public void Criar_CodigoLongoAcumulaComAsDemaisRecusas()
    {
        string identificacaoLonga = new('A', 150);

        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar("LEI", identificacaoLonga, "Descricao", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().Contain(
            [BaseLegalDoFadaErrorCodes.SemMunicipios, BaseLegalDoFadaErrorCodes.CodigoTamanho]);
        result.Errors.Single(e => e.Error.Code == BaseLegalDoFadaErrorCodes.CodigoTamanho).Error.Message.Should().Contain("80");
    }

    [Fact(DisplayName = "Identificação sem letra nem dígito é recusada pelo código")]
    public void Criar_IdentificacaoSoComPontuacao_RecusaOCodigo()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar("LEI", "§§ //", "Descricao", [MunicipioValido]);

        result.Errors.Select(e => e.Error.Code).Should().Contain(BaseLegalDoFadaErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Com o tipo inválido não há código a formar e só o tipo é recusado")]
    public void Criar_TipoInvalido_NaoTentaOCodigo()
    {
        Result<BaseLegalDoFada> result = BaseLegalDoFada.Criar("XYZ", "Lei 1", "Descricao", [MunicipioValido]);

        result.Errors.Select(e => e.Error.Code).Should().ContainSingle().Which.Should().Be(BaseLegalDoFadaErrorCodes.TipoInstrumentoInvalido);
    }
}
