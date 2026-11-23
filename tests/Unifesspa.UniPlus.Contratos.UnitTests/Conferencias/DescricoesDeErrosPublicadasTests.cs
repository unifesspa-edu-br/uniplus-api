namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;
using Unifesspa.UniPlus.TestSupport;

public sealed class DescricoesDeErrosPublicadasTests
{
    [Fact(DisplayName = "Cada código publicado tem uma descrição pública válida")]
    public void DescricoesConferemComOsCodigosDeErro()
    {
        string pasta = Path.Join(AppContext.BaseDirectory, "contracts");
        string caminho = Path.Join(pasta, "descricoes-de-erros.json");
        string[] codigos = [.. DocumentoDoCatalogoDeErros.Ler(File.ReadAllText(Path.Join(pasta, "codigos-de-erro.json")))
            .Codigos.Select(static entrada => entrada.Codigo)];
        File.Exists(caminho).Should().BeTrue("o contrato de descrições deve existir e ser escrito pelo autor");
        string[] divergencias = ConferirConteudo(File.ReadAllText(caminho), codigos);
        divergencias.Should().BeEmpty(string.Join('\n', divergencias));
    }

    internal static string[] ConferirConteudo(string conteudo, string[] codigos)
    {
        try
        {
            DocumentoDeDescricoesDeErros documento = DocumentoDeDescricoesDeErros.Ler(conteudo);
            string[] falhas = ConferenciaDasDescricoesDeErros.Conferir(documento, codigos);
            if (falhas.Length != 0)
                return falhas;
            return ConferirFormato(conteudo, documento.SerializarEmOrdem());
        }
        catch (JsonException)
        {
            return ["descricoes-de-erros.json: JSON inválido ou campo fora do formato."];
        }
    }
    private static string[] ConferirFormato(string conteudo, string canonico)
    {
        if (string.Equals(conteudo, canonico, StringComparison.Ordinal))
            return [];
        string resultado = ResultadoDeConferencia.Gravar("descricoes-de-erros.json", canonico);
        return [$"descricoes-de-erros.json: ordem ou formatação fora do padrão; {ResultadoDeConferencia.Mensagem(resultado, "contracts/descricoes-de-erros.json")}"];
    }
}
