namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos.ContratoDaConfiguracao;

/// <summary>
/// Os exemplos publicados e o rascunho incompleto validam contra o esquema da configuração do processo seletivo.
/// </summary>
public sealed class ExemplosDoEsquemaDaConfiguracaoTests
{
    public static TheoryData<string> Exemplos => [.. ArquivosDeExemplo];

    [Theory(DisplayName = "Cada exemplo publicado valida contra o esquema")]
    [MemberData(nameof(Exemplos))]
    public void Exemplo_Valida(string arquivo) =>
        CaminhosComErro(Carregar(Path.Join("exemplos", arquivo))).Should().BeEmpty();

    [Fact(DisplayName = "Há um exemplo com inscrição pelo sistema e outro de resultado importado")]
    public void Exemplos_CobremAsDuasOrigensDeCandidatos()
    {
        string[] origens = [.. ArquivosDeExemplo
            .Select(static arquivo => Carregar(Path.Join("exemplos", arquivo))["identificacao"]!["origemCandidatos"]!.GetValue<string>())];

        origens.Should().BeEquivalentTo(["inscricaoPropria", "importacaoExterna"]);
    }

    [Fact(DisplayName = "O rascunho só com a identificação e uma fase sem datas valida")]
    public void RascunhoIncompleto_Valida()
    {
        JsonNode rascunho = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "nome": "Processo seletivo em preparação" },
              "cronograma": { "fases": [ { "chave": "inscricao" } ] } }
            """);

        CaminhosComErro(rascunho).Should().BeEmpty();
    }
}
