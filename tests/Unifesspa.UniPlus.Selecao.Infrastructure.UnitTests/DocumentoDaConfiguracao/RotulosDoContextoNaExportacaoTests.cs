namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RotulosDoContextoNaExportacaoTests
{
    private const string Cronograma = "cronogramaFases.fases";

    /// <summary>
    /// Rótulos que o congelado guarda e que a versão 1 do esquema ainda não declara: cada item é o fim do caminho do rótulo
    /// no congelado, sem os índices das listas.
    /// </summary>
    private static readonly string[] ForaDaVersao1 =
    [
        $"{Cronograma}.origemDataRotulo",
        "alvo.tipoRotulo",
        "bancasRequeridas.codigoRotulo", "bancasRequeridas.papelRotulo", "bancasRequeridas.tiposDeDecisaoJulgadosRotulos",
        "etapas.bancas.codigoRotulo", "etapas.bancas.papelRotulo", "etapas.bancas.tiposDeDecisaoJulgadosRotulos",
        "documentosExigidos.definicoesDosDados.origemRotulo", "documentosExigidos.definicoesDosDados.quantidadeDeValoresRotulo",
        "documentosExigidos.definicoesDosDados.tipoRotulo",
        "calendarioDiasUteis.diasNaoUteis.abrangenciaRotulo",
        "formularios.termos.formaAceiteRotulo",
        "dadosDosFormularios.origemValoresRotulo", "dadosDosFormularios.tipoDeCampoRotulo",
        "gruposDosFormularios.agregados.operacaoRotulo", "gruposDosFormularios.subitens.origemValoresRotulo",
        "gruposDosFormularios.subitens.tipoDeCampoRotulo",
        "modalidades.composicaoVagasRotulo", "modalidades.naturezaLegalRotulo", "modalidades.regraRemanejamentoRotulo",
        "modalidades.regraDeAplicabilidade.operadorRotulo",
        "dadosCalculados.quantidadeDeValoresRotulo", "dadosCalculados.tipoRotulo",
        "dependenciasEntreItens.ligacoes.tipoRotulo",
    ];

    private static IEnumerable<string> Dispensados => ForaDaVersao1;

    [Theory]
    [InlineData("envelope.json")]
    [InlineData("envelope-rico.json")]
    [InlineData("envelope-cascata.json")]
    public void Exportar_RotuloDoCongelado_SaiNaExportacaoOuEstaForaDaVersao1(string arquivo)
    {
        (List<RotuloEncontrado> gravados, Dictionary<string, int> exportados) = Ler(arquivo);

        string[] faltantes = [.. gravados
            .Where(static rotulo => !Dispensados.Any(rotulo.TerminaEm))
            .GroupBy(static rotulo => rotulo.Assinatura)
            .Where(grupo => grupo.Count() > exportados.GetValueOrDefault(grupo.Key))
            .Select(grupo => $"{grupo.Key} (congelado {grupo.Count()}x, exportado {exportados.GetValueOrDefault(grupo.Key)}x)")];

        faltantes.Should().BeEmpty("cada rótulo do contexto sai na exportação, na mesma quantidade e com o mesmo texto, ou consta em ForaDaVersao1; faltam: {0}", string.Join(", ", faltantes));
    }

    [Fact]
    public void ListasDeRotulosDispensados_CadaItem_AindaExisteEmAlgumEnvelopeDeReferencia()
    {
        string[] caminhos = [.. new[] { "envelope.json", "envelope-rico.json", "envelope-cascata.json" }
            .SelectMany(static arquivo => RotulosDe(JsonNode.Parse(EnvelopesDeReferenciaDeTeste.Ler(arquivo))!))
            .Where(static rotulo => rotulo.EstaNoContexto)
            .Select(static rotulo => rotulo.Caminho)];

        IEnumerable<string> sobrando = Dispensados.Where(item => !caminhos.Any(caminho => TerminaEm(caminho, item)));

        sobrando.Should().BeEmpty("o que não existe mais em nenhum envelope não precisa estar na lista");
    }

    private static (List<RotuloEncontrado> Gravados, Dictionary<string, int> Exportados) Ler(string arquivo)
    {
        string congelado = EnvelopesDeReferenciaDeTeste.Ler(arquivo);
        JsonObject documento = new ProjecaoDaVersaoPublicada(new EsquemaDoDocumentoDaConfiguracao())
            .Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        List<RotuloEncontrado> gravados = [.. RotulosDe(JsonNode.Parse(congelado)!).Where(static rotulo => rotulo.EstaNoContexto)];
        Dictionary<string, int> exportados = RotulosDe(documento)
            .GroupBy(static rotulo => rotulo.Assinatura)
            .ToDictionary(static grupo => grupo.Key, static grupo => grupo.Count());
        return (gravados, exportados);
    }

    private static bool TerminaEm(string caminho, string fim) =>
        caminho == fim || caminho.EndsWith("." + fim, StringComparison.Ordinal);

    private static List<RotuloEncontrado> RotulosDe(JsonNode raiz)
    {
        List<RotuloEncontrado> encontrados = [];
        Percorrer(raiz, string.Empty, noContexto: false, encontrados);
        return encontrados;
    }

    private static void Percorrer(JsonNode? no, string caminho, bool noContexto, List<RotuloEncontrado> encontrados)
    {
        if (no is JsonArray lista)
        {
            foreach (JsonNode? item in lista)
            {
                Percorrer(item, caminho, noContexto, encontrados);
            }
        }
        else if (no is JsonObject objeto)
        {
            foreach ((string nome, JsonNode? valor) in objeto)
            {
                PercorrerPropriedade(nome, valor, caminho, noContexto, encontrados);
            }
        }
    }

    private static void PercorrerPropriedade(string nome, JsonNode? valor, string caminho, bool noContexto, List<RotuloEncontrado> encontrados)
    {
        string proprio = caminho.Length == 0 ? nome : $"{caminho}.{nome}";
        if (nome.EndsWith("Rotulo", StringComparison.Ordinal) || nome.EndsWith("Rotulos", StringComparison.Ordinal))
        {
            encontrados.Add(new(proprio, nome, valor?.ToJsonString() ?? "null", noContexto));
        }
        else
        {
            Percorrer(valor, nome == "contexto" ? caminho : proprio, noContexto || nome == "contexto", encontrados);
        }
    }

    private sealed record RotuloEncontrado(string Caminho, string Nome, string Valor, bool EstaNoContexto)
    {
        public string Assinatura => $"{Nome}={Valor}";

        public bool TerminaEm(string fim) => RotulosDoContextoNaExportacaoTests.TerminaEm(Caminho, fim);
    }
}
