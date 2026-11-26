namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Contratos;

using System.Globalization;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>
/// Todo campo de código do esquema que traz o rótulo ao lado (<c>{campo}Rotulo</c>, ou <c>{campo}Rotulos</c> quando o campo é uma
/// lista) tem na tabela de rótulos a entrada que o escreve, e toda entrada da tabela aponta para um campo que existe e rotula
/// todos os códigos que o esquema declara. A chave da entrada é o lugar do campo no esquema, no formato arquivo#ponteiro.
/// </summary>
public sealed class RotulosDosCodigosDeContratoNoEsquemaTests
{
    private const string SufixoDaLista = "Rotulos";
    private const string SufixoUnico = "Rotulo";

    // Rótulos que o esquema declara sem entrada na tabela, cada um com o motivo, guardados pelo lugar do rótulo no esquema.
    private static readonly Dictionary<string, string> RotulosSemEntrada = new(StringComparer.Ordinal)
    {
        ["motivo.schema.json#/properties/sentidoRotulo"] =
            "O sentido também é só de leitura e é escrito junto com o rótulo pelo cadastro de motivos.",
        ["pesos-por-area.schema.json#/properties/grupoAreaEnemRotulo"] =
            "O quadro de pesos por área é inteiro só de leitura, congelado do catálogo.",
    };

    private static readonly RotuloNoEsquema[] RotulosDoEsquema = [.. LerOsRotulosDoEsquema()];

    [Fact(DisplayName = "Todo rótulo só de leitura com o campo de código ao lado tem entrada na tabela ou exceção declarada")]
    public void RotuloComCodigoAoLado_TemEntradaNaTabelaOuExcecao()
    {
        string[] semEntrada =
        [
            .. RotulosComCodigo()
                .Where(static rotulo => !RotulosSemEntrada.ContainsKey(rotulo.Localizacao) && EntradasDo(rotulo).Count == 0)
                .Select(static rotulo => rotulo.Localizacao)
                .Distinct(StringComparer.Ordinal),
        ];

        semEntrada.Should().BeEmpty();
    }

    [Fact(DisplayName = "Toda entrada da tabela é o campo de código de algum rótulo do esquema")]
    public void EntradaDaTabela_EUsadaPorAlgumCampoDoEsquema()
    {
        HashSet<string> usadas = [.. RotulosComCodigo().SelectMany(static rotulo => rotulo.LocalizacoesDoCodigo)];

        string[] mortas = [.. RotulosDosCodigosDeContrato.Campos.Select(static entrada => entrada.Localizacao).Where(localizacao => !usadas.Contains(localizacao))];

        mortas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cada código que o esquema lista na enumeração do campo tem rótulo na tabela")]
    public void EnumeracaoDoEsquema_TemRotuloParaCadaCodigo()
    {
        string[] semRotulo = [.. RotulosDosCodigosDeContrato.Campos.SelectMany(static entrada => CodigosSemRotulo(entrada))];

        semRotulo.Should().BeEmpty();
    }

    [Fact(DisplayName = "O esquema declara rótulos e enumerações de código, para as regras acima não passarem vazias")]
    public void Esquema_DeclaraRotulosComEnumeracao()
    {
        RotulosComCodigo().Select(static rotulo => rotulo.Localizacao).Distinct(StringComparer.Ordinal).Should().HaveCountGreaterThan(30);
        RotulosDosCodigosDeContrato.Campos.SelectMany(static entrada => CodigosDaEnumeracao(entrada)).Should().HaveCountGreaterThan(30);
    }

    [Fact(DisplayName = "Cada exceção declarada ainda é um rótulo do esquema e traz o motivo")]
    public void ExcecaoDeclarada_AindaExisteNoEsquema()
    {
        string[] localizacoes = [.. RotulosDoEsquema.Select(static rotulo => rotulo.Localizacao)];

        RotulosSemEntrada.Keys.Where(excecao => !localizacoes.Contains(excecao)).Should().BeEmpty();
        RotulosSemEntrada.Values.Should().OnlyContain(static motivo => !string.IsNullOrWhiteSpace(motivo));
    }

    [Fact(DisplayName = "A tabela não repete o lugar de um campo no esquema")]
    public void Tabela_NaoRepeteOLugarDoCampo()
    {
        string[] repetidas =
        [
            .. RotulosDosCodigosDeContrato.Campos
                .GroupBy(static entrada => entrada.Localizacao, StringComparer.Ordinal)
                .Where(static grupo => grupo.Count() > 1)
                .Select(static grupo => grupo.Key),
        ];

        repetidas.Should().BeEmpty();
    }

    private static IEnumerable<RotuloNoEsquema> RotulosComCodigo() =>
        RotulosDoEsquema.Where(static rotulo => rotulo.SomenteLeitura && rotulo.Codigo is not null);

    private static List<CodigoDeContratoNoEsquema> EntradasDo(RotuloNoEsquema rotulo) =>
        [.. RotulosDosCodigosDeContrato.Campos.Where(entrada => rotulo.LocalizacoesDoCodigo.Contains(entrada.Localizacao, StringComparer.Ordinal))];

    private static IEnumerable<string> CodigosSemRotulo(CodigoDeContratoNoEsquema entrada) =>
        CodigosDaEnumeracao(entrada)
            .Where(codigo => entrada.RotuloDe(codigo) is null)
            .Select(codigo => $"{entrada.Localizacao}: o código '{codigo}' não tem rótulo");

    private static IEnumerable<string> CodigosDaEnumeracao(CodigoDeContratoNoEsquema entrada) =>
        RotulosComCodigo()
            .Where(rotulo => rotulo.LocalizacoesDoCodigo.Contains(entrada.Localizacao, StringComparer.Ordinal))
            .SelectMany(static rotulo => rotulo.LocalizacoesDoCodigo)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(static localizacao => ValoresDaEnumeracao(localizacao))
            .Distinct(StringComparer.Ordinal);

    private static IEnumerable<string> ValoresDaEnumeracao(string localizacao) =>
        TrechoDoEsquema(localizacao)?["enum"] is JsonArray valores
            ? valores.Select(static valor => valor?.GetValue<string>()).OfType<string>()
            : [];

    private static JsonNode? TrechoDoEsquema(string localizacao)
    {
        string[] partes = localizacao.Split('#', 2);
        JsonNode? raiz = JsonNode.Parse(File.ReadAllText(Path.Join(CaminhosDoDocumentoNoEsquema.DiretorioPublicado, partes[0])));
        return partes[1].Split('/', StringSplitOptions.RemoveEmptyEntries).Aggregate(raiz, static (atual, passo) => atual is JsonArray lista
            ? lista[int.Parse(passo, CultureInfo.InvariantCulture)]
            : atual?[passo]);
    }

    private static IEnumerable<RotuloNoEsquema> LerOsRotulosDoEsquema() =>
        NosDoEsquema(new EsquemaDoDocumentoDaConfiguracao().Forma).SelectMany(static no => no.Propriedades
            .Where(static propriedade => EhRotulo(propriedade.Key))
            .Select(propriedade => new RotuloNoEsquema(propriedade.Key, propriedade.Value, no.Propriedades.GetValueOrDefault(NomeDoCampo(propriedade.Key)))));

    // O esquema é recursivo: um nó pode voltar a conter a si mesmo, então cada nó é visitado uma vez, por referência.
    private static HashSet<FormaDoNo> NosDoEsquema(FormaDoNo raiz)
    {
        HashSet<FormaDoNo> visitados = new(ReferenceEqualityComparer.Instance);
        Stack<FormaDoNo> pendentes = new([raiz]);
        while (pendentes.TryPop(out FormaDoNo? no))
        {
            if (visitados.Add(no))
            {
                FilhosDe(no).ToList().ForEach(pendentes.Push);
            }
        }

        return visitados;
    }

    private static IEnumerable<FormaDoNo> FilhosDe(FormaDoNo no) =>
        no.Itens is null ? no.Propriedades.Values : no.Propriedades.Values.Append(no.Itens);

    private static bool EhRotulo(string nome) =>
        nome.EndsWith(SufixoUnico, StringComparison.Ordinal) || nome.EndsWith(SufixoDaLista, StringComparison.Ordinal);

    private static string NomeDoCampo(string nomeDoRotulo) =>
        nomeDoRotulo[..^(nomeDoRotulo.EndsWith(SufixoDaLista, StringComparison.Ordinal) ? SufixoDaLista : SufixoUnico).Length];

    private sealed record RotuloNoEsquema(string Nome, FormaDoNo Rotulo, FormaDoNo? Codigo)
    {
        public string Localizacao => Rotulo.Localizacoes[0];

        public bool SomenteLeitura => Rotulo.SomenteLeitura;

        // O código de uma lista de rótulos é o item da lista de códigos.
        public IReadOnlyList<string> LocalizacoesDoCodigo =>
            (Nome.EndsWith(SufixoDaLista, StringComparison.Ordinal) ? Codigo?.Itens ?? Codigo : Codigo)?.Localizacoes ?? [];
    }
}
