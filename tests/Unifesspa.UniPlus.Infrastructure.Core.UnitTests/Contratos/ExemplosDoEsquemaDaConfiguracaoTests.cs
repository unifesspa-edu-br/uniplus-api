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

        origens.Should().BeEquivalentTo(["INSCRICAO_PROPRIA", "IMPORTACAO_EXTERNA"]);
    }

    [Fact(DisplayName = "O rascunho só com a identificação e uma fase com a chave e a fase canônica, sem datas, valida")]
    public void RascunhoIncompleto_Valida()
    {
        JsonNode rascunho = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "nome": "Processo seletivo em preparação" },
              "cronograma": { "fases": [ { "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO" } ] } }
            """);

        CaminhosComErro(rascunho).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase sem a fase canônica é recusada no ponteiro da fase")]
    public void Fase_SemFaseCanonica_Recusa()
    {
        JsonNode rascunho = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "cronograma": { "fases": [ { "chave": "inscricao" } ] } }
            """);

        CaminhosComErro(rascunho).Should().Contain("/cronograma/fases/0");
    }

    [Fact(DisplayName = "A ordem na fase é recusada como propriedade desconhecida: a posição na lista é a ordem")]
    public void Fase_ComOrdem_Recusa()
    {
        JsonNode rascunho = Documento("""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "cronograma": { "fases": [ { "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "ordem": 1 } ] } }
            """);

        CaminhosComErro(rascunho).Should().Contain("/cronograma/fases/0/ordem");
    }
}
