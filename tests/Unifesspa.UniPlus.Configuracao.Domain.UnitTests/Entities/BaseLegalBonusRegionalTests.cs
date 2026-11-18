namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using Xunit;

public sealed class BaseLegalBonusRegionalTests
{
    private static readonly (string CodigoIbge, string Nome, string Uf) MunicipioValido = ("1504208", "Marabá", "PA");

    [Fact(DisplayName = "Criar com dados válidos retorna sucesso com os campos normalizados")]
    public void Criar_ComDadosValidos_RetornaSucesso()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI",
            "Lei Estadual 123",
            "Dispõe sobre o bônus regional",
            [MunicipioValido]);

        result.IsSuccess.Should().BeTrue();
        BaseLegalBonusRegional entity = result.Value!;
        entity.TipoInstrumento.Should().Be(TipoInstrumentoNormativo.Lei);
        entity.Identificacao.Should().Be("Lei Estadual 123");
        entity.Descricao.Should().Be("Dispõe sobre o bônus regional");
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Criar sem nenhum município retorna ValidationFailure com SemMunicipios")]
    public void Criar_SemMunicipio_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Lei 123", "Descricao", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.SemMunicipios);
    }

    [Fact(DisplayName = "Criar com código IBGE fora do formato de 7 dígitos retorna MunicipioInvalido")]
    public void Criar_ComCodigoIbgeInvalido_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Lei 123", "Descricao", [("123", "Marabá", "PA")]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.MunicipioInvalido);
    }

    [Fact(DisplayName = "Criar com tipo de instrumento fora do vocabulário fechado retorna TipoInstrumentoInvalido")]
    public void Criar_ComTipoInstrumentoInvalido_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "INVALIDO", "Lei 123", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido);
    }

    [Fact(DisplayName = "Criar com tipo de instrumento inválido reporta o erro no campo tipoInstrumento (nome da propriedade do contrato)")]
    public void Criar_ComTipoInstrumentoInvalido_ReportaErroNoCampoDoContrato()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "INVALIDO", "Lei 123", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e =>
            e.Error.Code == BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido && e.Field == "tipoInstrumento");
    }

    [Fact(DisplayName = "Criar com identificação vazia retorna IdentificacaoObrigatoria")]
    public void Criar_ComIdentificacaoVazia_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com identificação abaixo do tamanho mínimo retorna IdentificacaoTamanho")]
    public void Criar_ComIdentificacaoCurta_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "AB", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoTamanho);
    }

    [Fact(DisplayName = "Criar com identificação acima do tamanho máximo retorna IdentificacaoTamanho")]
    public void Criar_ComIdentificacaoLonga_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", new string('A', 501), "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoTamanho);
    }

    [Fact(DisplayName = "Criar com caractere nulo na identificação retorna IdentificacaoCaractereInvalido")]
    public void Criar_ComIdentificacaoContendoCaractereNulo_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Lei\0Estadual", "Descricao", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com quebra de linha na identificação retorna IdentificacaoCaractereInvalido")]
    public void Criar_ComIdentificacaoComQuebraDeLinha_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Lei\nEstadual", "Descricao", [MunicipioValido]);

        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com descrição de várias linhas e tabulação cria a base legal")]
    public void Criar_ComDescricaoDeVariasLinhas_Cria()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Identificacao", "Art. 1º\r\n\tArt. 2º", [MunicipioValido]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Descricao.Should().Be("Art. 1º\r\n\tArt. 2º");
    }

    [Fact(DisplayName = "Criar com controle na descrição retorna DescricaoCaractereInvalido")]
    public void Criar_ComDescricaoComControle_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Identificacao", "Descricao\u0007", [MunicipioValido]);

        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.DescricaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar com descrição vazia retorna DescricaoObrigatoria")]
    public void Criar_ComDescricaoVazia_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Identificacao", "", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "Criar com descrição acima do tamanho máximo retorna DescricaoTamanho")]
    public void Criar_ComDescricaoLonga_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Identificacao", new string('A', 2001), [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Criar com caractere nulo na descrição retorna DescricaoCaractereInvalido")]
    public void Criar_ComDescricaoContendoCaractereNulo_RetornaValidationFailure()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "LEI", "Identificacao", "Descricao\0com nulo", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.DescricaoCaractereInvalido);
    }

    [Fact(DisplayName = "Criar acumula todas as violações do payload no mesmo Result")]
    public void Criar_ComMultiplasViolacoes_AcumulaTodas()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar(
            "INVALIDO", "", "", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.IdentificacaoObrigatoria);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.DescricaoObrigatoria);
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.SemMunicipios);
    }

    [Fact(DisplayName = "Atualizar mantém as mesmas regras de domínio da criação")]
    public void Atualizar_MantemRegrasDeDominio()
    {
        BaseLegalBonusRegional entity = BaseLegalBonusRegional.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        Result result = entity.Atualizar("INVALIDO", "Ident", "Desc", [MunicipioValido]);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido);
    }

    [Fact(DisplayName = "Atualizar substitui integralmente a lista de municípios anterior")]
    public void Atualizar_SubstituiMunicipios()
    {
        BaseLegalBonusRegional entity = BaseLegalBonusRegional.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        (string CodigoIbge, string Nome, string Uf) novoMunicipio = ("1501402", "Belém", "PA");
        Result result = entity.Atualizar("LEI", "Identificacao", "Descricao", [novoMunicipio]);

        result.IsSuccess.Should().BeTrue();
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be("1501402");
    }

    [Fact(DisplayName = "Atualizar com payload inválido não altera o estado anterior da entidade")]
    public void Atualizar_ComPayloadInvalido_NaoAlteraEstadoAnterior()
    {
        BaseLegalBonusRegional entity = BaseLegalBonusRegional.Criar("LEI", "Identificacao", "Descricao", [MunicipioValido]).Value!;

        Result result = entity.Atualizar("LEI", "Identificacao", "Descricao", []);

        result.IsFailure.Should().BeTrue();
        entity.Municipios.Should().HaveCount(1);
        entity.Municipios[0].CodigoIbge.Should().Be(MunicipioValido.CodigoIbge);
    }

    [Fact(DisplayName = "Criar forma o código estável a partir do tipo e da identificação")]
    public void Criar_FormaOCodigo()
    {
        BaseLegalBonusRegional entity = BaseLegalBonusRegional.Criar(
            "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Descricao", [MunicipioValido]).Value!;

        entity.Codigo.Valor.Should().Be("PORTARIA_UNIFESSPA_NO_2514_2023");
    }

    [Fact(DisplayName = "Atualizar tipo e identificação mantém o código da criação")]
    public void Atualizar_MantemOCodigo()
    {
        BaseLegalBonusRegional entity = BaseLegalBonusRegional.Criar("LEI", "Lei 123", "Descricao", [MunicipioValido]).Value!;

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

        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar("LEI", identificacaoLonga, "Descricao", []);

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(e => e.Error.Code).Should().Contain(
            [BaseLegalBonusRegionalErrorCodes.SemMunicipios, BaseLegalBonusRegionalErrorCodes.CodigoTamanho]);
        result.Errors.Single(e => e.Error.Code == BaseLegalBonusRegionalErrorCodes.CodigoTamanho).Error.Message.Should().Contain("100");
    }

    [Fact(DisplayName = "Identificação sem letra nem dígito é recusada pelo código")]
    public void Criar_IdentificacaoSoComPontuacao_RecusaOCodigo()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar("LEI", "§§ //", "Descricao", [MunicipioValido]);

        result.Errors.Select(e => e.Error.Code).Should().Contain(BaseLegalBonusRegionalErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Com o tipo inválido não há código a formar e só o tipo é recusado")]
    public void Criar_TipoInvalido_NaoTentaOCodigo()
    {
        Result<BaseLegalBonusRegional> result = BaseLegalBonusRegional.Criar("XYZ", "Lei 1", "Descricao", [MunicipioValido]);

        result.Errors.Select(e => e.Error.Code).Should().ContainSingle().Which.Should().Be(BaseLegalBonusRegionalErrorCodes.TipoInstrumentoInvalido);
    }
}
