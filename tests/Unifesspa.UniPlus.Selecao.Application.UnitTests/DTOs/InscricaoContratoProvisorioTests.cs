namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.DTOs;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Testes.Compartilhado;

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
        InscricaoRequest? request = JsonSerializer.Deserialize<InscricaoRequest>(ExemplosDoContratoDeInscricao.Rascunho, Opcoes);

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
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.Inscricao, Opcoes)!;

        json["situacao"]!.GetValue<string>().Should().Be("submetida", "enums serializam em camelCase, como o host configura");
        json["numeroInscricao"]!.GetValue<string>().Should().Be("000123");
        json["formulario"]!["respostas"]!["RG_NUMERO"]!.GetValue<string>().Should().Be("1234567",
            "o RG volta dentro de respostas, decifrado — não existe campo próprio de documento no contrato");
    }

    [Fact]
    public void InscricaoDto_Exemplo_NaoExpoeCpf()
    {
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.Inscricao, Opcoes)!;

        json.AsObject().Should().NotContainKey("cpf", "o CPF nunca sai no contrato: já está no token, e o comprovante o devolve mascarado");
    }

    [Fact]
    public void InscricaoDto_Exemplo_MostraEscolhasDaVersaoFixada()
    {
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.Inscricao, Opcoes)!;

        json["escolhasDeCurso"]!["ofertaCursoSegundaOpcaoId"]!.GetValue<string>().Should().Be("0199e000-0000-7000-8000-000000000003");
        json["escolhasDeCurso"]!["numeroDeOpcoesDeCurso"]!.GetValue<int>().Should().Be(2, "o número vem da versão fixada da inscrição");
        json["escolhasDeCurso"]!["ofertas"]!.AsArray().Should().HaveCount(2, "o portal escolhe a opção entre as ofertas da versão fixada, não do certame vivo");
    }

    [Fact(DisplayName = "ComprovanteDaInscricaoDto: o exemplo traz o cartão do candidato sem número de documento e o arquivo sem nome nem local")]
    public void ComprovanteDaInscricaoDto_Exemplo_NaoCarregaDocumento()
    {
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.Comprovante, Opcoes)!;

        json.AsObject().Select(static p => p.Key).Should().BeEquivalentTo(["candidato", "inscricao", "processo", "escolhasDeCurso", "formulario"]);
        json.AsObject().Should().NotContainKey("rgNumero").And.NotContainKey("documentoEstrangeiroNumero").And.NotContainKey("cpf");
        JsonNode arquivo = json["formulario"]!["documentos"]![0]!["arquivos"]![0]!;
        arquivo.AsObject().Select(static p => p.Key).Should().BeEquivalentTo(["hashSha256", "tamanhoBytes", "formato"],
            "o comprovante identifica o arquivo sem revelar nome nem local de armazenamento");
        json["inscricao"]!["envio"]!["hashIntegridade"]!.GetValue<string>().Should().Be("sha256-def");
        json["candidato"]!["cpfMascarado"]!.GetValue<string>().Should().Be(PersonasDeTeste.FernandaDrumond.Mascarado);
        json["inscricao"]!["numeroAnterior"].Should().BeNull("o primeiro comprovante não tem anterior");
        json["inscricao"]!["vale"]!.GetValue<bool>().Should().BeFalse();
        json["inscricao"]!["prazoDaCorrecao"]!["aberto"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "MinhaInscricaoDto: o exemplo mostra os cursos escolhidos e não repete nome nem CPF do titular")]
    public void MinhaInscricaoDto_Exemplo_MostraOsCursosEscolhidos()
    {
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.MinhaInscricao, Opcoes)!;

        json["opcoesDeCurso"]!.AsArray().Select(static o => o!["cursoNome"]!.GetValue<string>()).Should().Equal("Medicina", "Direito");
    }

    [Fact]
    public void MinhaInscricaoDto_Exemplo_NaoExpoeDadosDoTitular()
    {
        JsonNode json = JsonSerializer.SerializeToNode(ExemplosDoContratoDeInscricao.MinhaInscricao, Opcoes)!;

        json.AsObject().Should().NotContainKey("nomePreferido").And.NotContainKey("cpfMascarado");
    }
}
