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
