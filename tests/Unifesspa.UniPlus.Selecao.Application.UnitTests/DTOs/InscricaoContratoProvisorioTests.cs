namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

/// <summary>
/// O contrato provisório da inscrição do candidato (issue #1858, primeiro commit): cada exemplo
/// aqui é o que o web recebe para desenvolver com mocks antes dos endpoints reais existirem (a2 de
/// #1859). Diferente de um arquivo JSON solto, o exemplo vive dentro do teste — se um DTO mudar de
/// forma sem atualizar o exemplo, o teste falha em vez de o contrato ficar desatualizado em
/// silêncio. As opções de serialização reproduzem exatamente as do host (<c>Program.cs</c>):
/// camelCase em propriedades e em enums.
/// </summary>
public sealed class InscricaoContratoProvisorioTests
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact(DisplayName = "InscricaoRequest: o exemplo do rascunho desserializa com as duas opções de curso, respostas, grupo e aceite")]
    public void InscricaoRequest_ExemploDoRascunho_Desserializa()
    {
        const string Exemplo = """
        {
            "ofertaCursoId": "0199e000-0000-7000-8000-000000000001",
            "ofertaCursoSegundaOpcaoId": "0199e000-0000-7000-8000-000000000003",
            "respostas": {
                "NOME_SOCIAL": "Ana",
                "RG_NUMERO": "1234567"
            },
            "grupos": {
                "COMPOSICAO_FAMILIAR": [
                    { "id": "membro-1", "respostas": { "PARENTESCO": "MAE" } }
                ]
            },
            "aceites": [
                { "codigo": "TERMO_VERACIDADE", "versaoId": "0199e000-0000-7000-8000-000000000002", "hashVersao": "sha256-abc" }
            ]
        }
        """;

        InscricaoRequest? request = JsonSerializer.Deserialize<InscricaoRequest>(Exemplo, Opcoes);

        request.Should().NotBeNull();
        request!.OfertaCursoId.Should().Be(Guid.Parse("0199e000-0000-7000-8000-000000000001"));
        request.OfertaCursoSegundaOpcaoId.Should().Be(Guid.Parse("0199e000-0000-7000-8000-000000000003"));
        request.Respostas.Should().ContainKey("RG_NUMERO");
        request.Grupos.Should().ContainKey("COMPOSICAO_FAMILIAR")
            .WhoseValue.Should().ContainSingle(o => o.Id == "membro-1");
        request.Aceites.Should().ContainSingle(a => a.Codigo == "TERMO_VERACIDADE");
    }

    [Fact(DisplayName = "InscricaoDto: o exemplo da inscrição submetida serializa em camelCase, com a situação como texto")]
    public void InscricaoDto_ExemploSubmetida_SerializaEmCamelCase()
    {
        InscricaoDto dto = new(
            Id: Guid.Parse("0199e000-0000-7000-8000-000000000010"),
            ProcessoSeletivoId: Guid.Parse("0199e000-0000-7000-8000-000000000011"),
            Situacao: SituacaoInscricao.Submetida,
            NumeroInscricao: "000123",
            VersaoFormularioId: Guid.Parse("0199e000-0000-7000-8000-000000000012"),
            OfertaCursoId: Guid.Parse("0199e000-0000-7000-8000-000000000001"),
            OfertaCursoSegundaOpcaoId: Guid.Parse("0199e000-0000-7000-8000-000000000003"),
            NumeroDeOpcoesDeCurso: 2,
            Ofertas:
            [
                new OfertaDeCursoCertameDto(Guid.Parse("0199e000-0000-7000-8000-000000000001"), "Direito", "Bacharelado", "Campus de Marabá", ["MATUTINO"]),
                new OfertaDeCursoCertameDto(Guid.Parse("0199e000-0000-7000-8000-000000000003"), "Engenharia da Computação", "Bacharelado", "Campus de Marabá", ["VESPERTINO"]),
            ],
            Respostas: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ana"),
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
            },
            Derivados: new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Grupos: new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal),
            Aceites: [new AceiteDeTermoInput("TERMO_VERACIDADE", Guid.Parse("0199e000-0000-7000-8000-000000000002"), "sha256-abc")],
            ETag: "\"42\"",
            Avaliacao: new AvaliacaoDaInscricaoDto(new ModalidadesDaInscricaoDto(
                Derivada: true, Situacao: "CONCORRE", [new ModalidadeDaInscricaoDto("AC", "Ampla concorrência", 0)], []),
                new DocumentosDaInscricaoDto([])));

        JsonNode json = JsonSerializer.SerializeToNode(dto, Opcoes)!;

        json["situacao"]!.GetValue<string>().Should().Be("submetida", "enums serializam em camelCase, como o host configura");
        json["numeroInscricao"]!.GetValue<string>().Should().Be("000123");
        json["respostas"]!["RG_NUMERO"]!.GetValue<string>().Should().Be("1234567",
            "o RG volta dentro de respostas, decifrado — não existe campo próprio de documento no contrato");
        json.AsObject().Should().NotContainKey("cpf", "o CPF nunca sai no contrato: já está no token, e o comprovante o devolve mascarado");
        json["ofertaCursoSegundaOpcaoId"]!.GetValue<string>().Should().Be("0199e000-0000-7000-8000-000000000003");
        json["numeroDeOpcoesDeCurso"]!.GetValue<int>().Should().Be(2, "o número vem da versão fixada da inscrição");
        json["ofertas"]!.AsArray().Should().HaveCount(2, "o portal escolhe a opção entre as ofertas da versão fixada, não do certame vivo");
    }

    [Fact(DisplayName = "ComprovanteInscricaoDto: o exemplo não carrega nenhum número de documento")]
    public void ComprovanteInscricaoDto_Exemplo_NaoCarregaDocumento()
    {
        ComprovanteInscricaoDto dto = new(
            CpfMascarado: "***.456.789-**",
            NumeroInscricao: "000123",
            Instante: new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero),
            FusoHorario: FusoHorarioDto.Institucional,
            HashIntegridade: "sha256-def",
            OfertaCursoId: Guid.Parse("0199e000-0000-7000-8000-000000000001"),
            OfertaCursoSegundaOpcaoId: null,
            Respostas: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ana"),
            },
            Derivados: new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            Grupos: new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal),
            Aceites: [new AceiteDeTermoInput("TERMO_VERACIDADE", Guid.Parse("0199e000-0000-7000-8000-000000000002"), "sha256-abc")],
            Modalidades: [new ModalidadeDaInscricaoDto("AC", "Ampla concorrência", 0)],
            ModalidadesSegundaOpcao: null);

        JsonNode json = JsonSerializer.SerializeToNode(dto, Opcoes)!;

        json.AsObject().Should().NotContainKey("rgNumero").And.NotContainKey("documentoEstrangeiroNumero");
        json["hashIntegridade"]!.GetValue<string>().Should().Be("sha256-def");
    }

    [Fact(DisplayName = "MinhaInscricaoDto: o exemplo mostra o nome preferido, não o nome civil")]
    public void MinhaInscricaoDto_Exemplo_MostraNomePreferido()
    {
        MinhaInscricaoDto dto = new(
            InscricaoId: Guid.Parse("0199e000-0000-7000-8000-000000000012"),
            ProcessoSeletivoId: Guid.Parse("0199e000-0000-7000-8000-000000000011"),
            NomeDoProcesso: "PSS Medicina 2027",
            NumeroInscricao: "000123",
            Situacao: SituacaoInscricao.Submetida,
            NomePreferido: "Ana (nome social)",
            CpfMascarado: "***.456.789-**",
            CanceladaEm: null,
            MotivoDoCancelamento: null,
            Acoes: new AcoesDaInscricaoDto(PodeCorrigir: true, PodeCancelar: true, PodeDescartar: false),
            Comprovantes: [new ComprovanteEmitidoDto(1, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero))]);

        JsonNode json = JsonSerializer.SerializeToNode(dto, Opcoes)!;

        json["nomePreferido"]!.GetValue<string>().Should().Be("Ana (nome social)");
    }
}
