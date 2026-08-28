namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Linq;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CampusTests
{
    private static readonly DateTimeOffset Agora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private static ReferenciaEnderecoGeo Endereco(string cidadeCodigoIbge = "1504208", string cidadeUf = "PA") =>
        ReferenciaEnderecoGeo.Criar(
            "68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            cidadeCodigoIbge, "Marabá", cidadeUf, -5.3m, -49.1m,
            NivelResolucaoEndereco.Logradouro, "logradouro", Agora).Value!;

    [Fact(DisplayName = "Criar com dados válidos persiste a referência de cidade e o display cache")]
    public void Criar_DadosValidos_PreencheReferenciaCidade()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "12345");

        resultado.IsSuccess.Should().BeTrue();
        Campus campus = resultado.Value!;
        campus.Sigla.Should().Be("CAMAR", "a sigla é normalizada para uppercase");
        campus.Nome.Should().Be("Campus Marabá");
        campus.CidadeCodigoIbge.Should().Be("1504208");
        campus.CidadeNome.Should().Be("Marabá");
        campus.CidadeUf.Should().Be("PA");
        campus.CidadeOrigem.Should().Be("geo-api");
        campus.CidadeDisplayAtualizadoEm.Should().Be(Agora);
        campus.Endereco.Should().BeNull("nenhum endereço estruturado foi informado");
    }

    [Fact(DisplayName = "Criar com endereço estruturado coerente persiste o endereço")]
    public void Criar_ComEnderecoCoerente_PersisteEndereco()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(), null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Endereco!.Cep.Should().Be("68507590");
        resultado.Value!.Endereco!.CidadeCodigoIbge.Should().Be("1504208");
    }

    [Fact(DisplayName = "Criar com endereço de cidade incoerente com a cidade do campus falha (CA-04)")]
    public void Criar_EnderecoCidadeIncoerente_Falha()
    {
        // Endereço resolvido em Belém (1501402) num campus de Marabá (1504208).
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, Endereco(cidadeCodigoIbge: "1501402"), null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(EnderecoReferenciaErrorCodes.CidadeIncoerente);
    }

    [Fact(DisplayName = "Criar com código IBGE malformado falha com erro de formato de cidade")]
    public void Criar_CodigoIbgeMalformado_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "150420", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Criar com UF incoerente com o prefixo do código falha")]
    public void Criar_UfIncoerente_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "SP",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar sem sigla falha")]
    public void Criar_SemSigla_Falha()
    {
        Result<Campus> resultado = Campus.Criar(
            "  ", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    // ─── ADR-0125: acumulação multi-campo (fonte única é o domínio) ───────

    [Fact(DisplayName = "Criar com Sigla e Nome vazios ao mesmo tempo acumula as duas violações")]
    public void Criar_SiglaENomeVazios_AcumulaAsDuasViolacoes()
    {
        Result<Campus> resultado = Campus.Criar(
            "", "", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("sigla");
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(CampusErrorCodes.NomeObrigatorio);

        // Error (mono-erro) preserva compatibilidade: aponta para a primeira violação.
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com Sigla nula (não só vazia) é tratado como obrigatório, não lança")]
    public void Criar_SiglaNula_NaoLancaETrataComoObrigatorio()
    {
        // Sem validator FluentValidation garantindo não-nulo a montante (ADR-0125),
        // um payload que desserializa Sigla como null precisa continuar virando
        // 422 sigla_obrigatoria — nunca uma ArgumentNullException/500.
        Result<Campus> resultado = Campus.Criar(
            null, "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
    }

    [Fact(DisplayName = "Criar com Sigla vazia e cidade malformada acumula as duas violações de fontes diferentes")]
    public void Criar_SiglaVaziaECidadeMalformada_AcumulaViolacoesDeFontesDiferentes()
    {
        // Sigla é regra própria do Campus; cidade delega a ReferenciaCidadeGeo — as
        // duas precisam aparecer juntas no mesmo lote quando ambas violam.
        Result<Campus> resultado = Campus.Criar(
            "", "Campus Marabá", "150420", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaObrigatoria);
        resultado.Errors[1].Error.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Criar com código, nome e UF da cidade ausentes ao mesmo tempo acumula as três violações rotuladas")]
    public void Criar_TrioDeCidadeTotalmenteAusente_AcumulaAsTresViolacoesRotuladas()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "", "", "",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors.Select(e => e.Field).Should().BeEquivalentTo(
            ["cidadeCodigoIbge", "cidadeNome", "cidadeUf"]);
    }

    [Fact(DisplayName = "Criar com nome da cidade ausente rotula o campo como cidadeNome, não cidadeCodigoIbge")]
    public void Criar_CidadeNomeAusente_RotulaCampoCorreto()
    {
        // ReferenciaCidadeGeo.Validar cobre código IBGE, nome e UF na mesma chamada —
        // CampoDaCidade precisa mapear por sub-código para não rotular toda falha de
        // cidade com o mesmo campo fixo.
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("cidadeNome");
        resultado.Errors[0].Error.Code.Should().Be(CidadeReferenciaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Criar com UF da cidade ausente rotula o campo como cidadeUf")]
    public void Criar_CidadeUfAusente_RotulaCampoCorreto()
    {
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Field.Should().Be("cidadeUf");
        resultado.Errors[0].Error.Code.Should().Be(CidadeReferenciaErrorCodes.UfObrigatoria);
    }

    [Fact(DisplayName = "Criar com código e-MEC só de espaços é aceito e normalizado para nulo")]
    public void Criar_CodigoEmecSoDeEspacos_NormalizaParaNulo()
    {
        // codigoEmec.Trim().Length > CodigoEmecMaxLength nunca dispara para uma
        // string só de espaços (Trim() zera) — comportamento intencional: tratado
        // como "não informado", igual a string vazia ou nulo.
        Result<Campus> resultado = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, "   ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CodigoEmec.Should().BeNull();
    }

    // ─── Acentuação gráfica na sigla (issue #1304) ───────────────────────

    [Theory(DisplayName = "Criar aceita sigla sem acentuação e a normaliza para maiúsculas")]
    [InlineData("CMB", "CMB")]
    [InlineData("CMRB-I", "CMRB-I")]
    [InlineData("camar", "CAMAR")]
    [InlineData("Campus-2", "CAMPUS-2")]
    public void Criar_SiglaSemAcentuacao_AceitaENormalizaParaMaiusculas(string informada, string esperada)
    {
        // CA-05/CA-06: letras sem acentuação, números e hífen continuam válidos, e o
        // ToUpperInvariant segue rodando depois da validação.
        Result<Campus> resultado = Campus.Criar(
            informada, "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be(esperada);
    }

    [Theory(DisplayName = "Criar recusa sigla com acentuação gráfica sem transformá-la")]
    [InlineData("CÁMAR")]
    [InlineData("cámar")]
    [InlineData("CAMARÃ")]
    [InlineData("CEDILHÇ")]
    public void Criar_SiglaComAcentuacaoPrecomposta_Recusa(string sigla)
    {
        // CA-01/CA-04: recusa em vez de remover o acento — o valor informado não é
        // corrigido para a versão sem diacrítico.
        Result<Campus> resultado = Campus.Criar(
            sigla, "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Value.Should().BeNull();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("sigla");
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
        resultado.Errors[0].Error.Message.Should().NotContain(sigla,
            "ADR-0023: a mensagem não devolve o valor rejeitado (CA-10)");
    }

    [Fact(DisplayName = "Criar recusa sigla cujo acento chega como marca combinante, com o mesmo código")]
    public void Criar_SiglaComMarcaCombinante_RecusaComOMesmoCodigo()
    {
        // CA-03: "CÁMAR" em NFD — 'A' seguido de U+0301 (COMBINING ACUTE ACCENT).
        // Nenhuma normalização prévia acontece: a string chega decomposta ao domínio.
        const string siglaDecomposta = "CA\u0301MAR";
        siglaDecomposta.Should().HaveLength(6, "a marca combinante é um caractere à parte");
        siglaDecomposta.Should().NotBe("CÁMAR", "o valor está em NFD, não em NFC");

        Result<Campus> resultado = Campus.Criar(
            siglaDecomposta, "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be("sigla");
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
    }

    [Fact(DisplayName = "Criar com sigla acentuada e nome vazio acumula as duas violações")]
    public void Criar_SiglaAcentuadaENomeVazio_AcumulaAsDuasViolacoes()
    {
        // CA-08: a violação de acentuação participa do mesmo lote das demais
        // violações independentes (ADR-0125).
        Result<Campus> resultado = Campus.Criar(
            "CÁMAR", "", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Field.Should().Be("sigla");
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(CampusErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Atualizar recusa a introdução de acentuação e preserva a sigla armazenada")]
    public void Atualizar_IntroduzAcentuacao_RecusaEPreservaSiglaArmazenada()
    {
        // CA-02: a mesma regra vale na atualização; o agregado não é mutado, então a
        // sigla persistida continua valendo mesmo com o Wolverine chamando
        // SaveChangesAsync depois do handler retornar falha.
        Campus campus = Campus.Criar(
            "CAMAR", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        Result resultado = campus.Atualizar(
            "CÁMAR", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Field.Should().Be("sigla");
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
        campus.Sigla.Should().Be("CAMAR");
    }

    [Fact(DisplayName = "ValidarAtualizacao recusa sigla acentuada sem instanciar o agregado")]
    public void ValidarAtualizacao_SiglaAcentuada_Recusa()
    {
        // O handler de atualização valida por aqui antes de buscar o agregado — a
        // regra precisa valer nesse caminho também, não só em Atualizar.
        Result resultado = Campus.ValidarAtualizacao(
            "CÁMAR", "Campus Marabá", "1504208", "Marabá", "PA", null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
    }

    [Fact(DisplayName = "Criar com sigla longa demais reporta o tamanho, não a acentuação")]
    public void Criar_SiglaAcentuadaEAcimaDoTamanho_ReportaTamanho()
    {
        // Regras encadeadas do mesmo campo: obrigatoriedade → tamanho → acentuação.
        // A causa mais básica sai primeiro para orientar a correção.
        Result<Campus> resultado = Campus.Criar(
            new string('Á', 21), "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaTamanho);
    }

    [Fact(DisplayName = "Criar recusa diacrítico fora do plano básico, que uma checagem char a char deixaria passar")]
    public void Criar_DiacriticoForaDoPlanoBasico_Recusa()
    {
        // U+1D167 (MUSICAL SYMBOL COMBINING TREMOLO-1) é marca sem avanço de largura
        // e chega como par substituto: percorrer a sigla por char classificaria as
        // duas metades como Surrogate e aceitaria o valor. Percorrer por Rune, não.
        Result<Campus> resultado = Campus.Criar(
            "CAM\U0001D167AR", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
    }

    [Fact(DisplayName = "Criar recusa marca invisível que compartilha a categoria dos diacríticos")]
    public void Criar_MarcaInvisivel_Recusa()
    {
        // U+FE0F (VARIATION SELECTOR-16) não acentua nada, mas é marca sem avanço de
        // largura e cai na mesma regra. Recusar é o resultado desejado — um caractere
        // invisível na sigla produz justamente a variação visualmente indistinguível
        // que a regra existe para evitar. Teste fixa a decisão para que ela não seja
        // desfeita por engano.
        Result<Campus> resultado = Campus.Criar(
            "CAMAR\uFE0F", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors[0].Error.Code.Should().Be(CampusErrorCodes.SiglaAcentuacaoInvalida);
    }

    [Fact(DisplayName = "Criar com sigla contendo par substituto malformado não lança")]
    public void Criar_SiglaComParSubstitutoMalformado_NaoLanca()
    {
        // String.Normalize lançaria ArgumentException diante de um surrogate solto —
        // a checagem de acentuação precisa tolerá-lo para que a requisição continue
        // sendo tratada pelas regras de campo, e não vire 500.
        Result<Campus> resultado = Campus.Criar(
            "CAM\ud800AR", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsSuccess.Should().BeTrue("um surrogate solto não é acentuação gráfica");
    }

    [Fact(DisplayName = "Atualizar troca os campos e mantém validação")]
    public void Atualizar_DadosValidos_Aplica()
    {
        Campus campus = Campus.Criar(
            "CAMar", "Campus Marabá", "1504208", "Marabá", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null).Value!;

        Result resultado = campus.Atualizar(
            "CABel", "Campus Belém", "1501402", "Belém", "PA",
            ReferenciaCidadeGeo.OrigemGeoApi, Agora, null, null);

        resultado.IsSuccess.Should().BeTrue();
        campus.Sigla.Should().Be("CABEL");
        campus.CidadeCodigoIbge.Should().Be("1501402");
        campus.CidadeNome.Should().Be("Belém");
    }
}
