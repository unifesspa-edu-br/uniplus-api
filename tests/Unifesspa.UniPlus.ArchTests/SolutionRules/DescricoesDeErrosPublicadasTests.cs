namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

public sealed class DescricoesDeErrosPublicadasTests
{
    internal const string VariavelDeReformatacao = "UPDATE_DESCRICOES_DE_ERRO_BASELINE";
    private const string ArquivoDeDescricoes = "contracts/descricoes-de-erros.json";
    private const string ArquivoDeCodigos = "contracts/codigos-de-erro.json";

    [Fact(DisplayName = "Cada código publicado tem uma descrição pública válida")]
    public void DescricoesConferemComOsCodigosDeErro()
    {
        string raiz = SolutionRootLocator.Locate();
        string caminho = Path.Join(raiz, ArquivoDeDescricoes);
        string[] codigos = [.. DocumentoDoCatalogoDeErros.Ler(File.ReadAllText(Path.Join(raiz, ArquivoDeCodigos)))
            .Codigos.Select(static entrada => entrada.Codigo)];
        bool reformatar = Environment.GetEnvironmentVariable(VariavelDeReformatacao) == "1";

        File.Exists(caminho).Should().BeTrue("o contrato de descrições deve existir");
        string[] divergencias = ConferirArquivo(caminho, codigos, reformatar);
        divergencias.Should().BeEmpty(string.Join('\n', divergencias));
    }

    internal static string[] ConferirArquivo(string caminho, string[] codigos, bool reformatar)
    {
        string conteudo = File.ReadAllText(caminho);
        return ConferirConteudo(conteudo, codigos, reformatar ? canonico => File.WriteAllText(caminho, canonico) : null);
    }

    internal static string[] ConferirConteudo(string conteudo, string[] codigos, Action<string>? gravarCanonico)
    {
        try
        {
            DocumentoDeDescricoesDeErros documento = DocumentoDeDescricoesDeErros.Ler(conteudo);
            return
            [
                .. ConferenciaDasDescricoesDeErros.Conferir(documento, codigos),
                .. ConferirFormato(conteudo, documento.SerializarEmOrdem(), gravarCanonico),
            ];
        }
        catch (JsonException)
        {
            return ["descricoes-de-erros.json: JSON inválido ou campo fora do formato."];
        }
    }

    private static string[] ConferirFormato(string conteudo, string canonico, Action<string>? gravarCanonico)
    {
        if (string.Equals(conteudo, canonico, StringComparison.Ordinal))
            return [];
        if (gravarCanonico is null)
            return [$"descricoes-de-erros.json: ordem ou formatação fora do padrão. Use {VariavelDeReformatacao}=1."];
        gravarCanonico(canonico);
        return [];
    }
}
