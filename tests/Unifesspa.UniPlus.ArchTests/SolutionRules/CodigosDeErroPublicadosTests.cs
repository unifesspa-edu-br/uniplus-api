namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// O arquivo <c>contracts/codigos-de-erro.json</c> é a lista dos códigos de erro de fio que a API
/// emite, e é gerado dos registros de erro dos módulos e das definições da fronteira HTTP. O
/// lint do catálogo de erros do portal lê o arquivo para conferir que cada código tem página.
/// Código novo, código removido, ou status e título alterados reprovam até o arquivo ser regerado.
/// </summary>
public sealed class CodigosDeErroPublicadosTests
{
    private const string AtualizarArquivoEnvVar = "UPDATE_CODIGOS_DE_ERRO_BASELINE";
    private const string ArquivoPublicado = "contracts/codigos-de-erro.json";

    private static readonly Lazy<DocumentoDoCatalogoDeErros> Gerado = new(() =>
        DocumentoDoCatalogoDeErros.Gerar(AgregadorDoCatalogoDeErros.Agregar(RegistrosDeErroDaSolucao.Instanciar())));

    [Fact(DisplayName = "O arquivo de códigos de erro bate com os códigos que a API emite")]
    public void ArquivoPublicadoBateComOsCodigosEmitidos()
    {
        string caminho = Path.Join(SolutionRootLocator.Locate(), ArquivoPublicado);
        string gerado = Gerado.Value.Serializar();
        RegerarSeSolicitado(caminho, gerado);

        string publicado = LerPublicado(caminho);
        IReadOnlyList<string> divergencias = DivergenciasDoCatalogoDeErros.Comparar(
            DocumentoDoCatalogoDeErros.Ler(publicado).Codigos, Gerado.Value.Codigos);

        divergencias.Should().BeEmpty(ComoRegerar(divergencias));
        publicado.Should().Be(gerado, $"as entradas conferem, mas o arquivo não é o que o gerador produz (versão do formato, ordem ou formatação). {ComoRegerar([])}");
    }

    [Fact(DisplayName = "Toda definição da fronteira HTTP está no catálogo de códigos de erro")]
    public void TodaDefinicaoDaFronteiraHttpEstaNoCatalogo()
    {
        EntradaDoCatalogoDeErros[] definicoes =
        [
            .. typeof(ErrosDaFronteiraHttp)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(static campo => campo.FieldType == typeof(DomainErrorMapping))
                .Select(static campo => (DomainErrorMapping)campo.GetValue(null)!)
                .Select(static d => new EntradaDoCatalogoDeErros(d.Code, d.Status, d.Title)),
        ];

        definicoes.Should().NotBeEmpty("sem definição descoberta, a conferência passaria vazia");
        Gerado.Value.Codigos.Should().Contain(
            definicoes,
            "o emissor da fronteira lê a definição de ErrosDaFronteiraHttp, e o registro dela precisa listá-la");
    }

    [Fact(DisplayName = "Todo código de erro da API começa com o prefixo uniplus.")]
    public void TodoCodigoComecaComOPrefixoDaPlataforma()
    {
        string[] foraDoPadrao =
        [
            .. Gerado.Value.Codigos.Select(static e => e.Codigo)
                .Where(static codigo => !codigo.StartsWith("uniplus.", StringComparison.Ordinal)),
        ];

        foraDoPadrao.Should().BeEmpty("o código de fio é uniplus.<módulo>.<assunto>.<motivo>, e o lint do portal parte desse prefixo");
    }

    [Fact(DisplayName = "Todo módulo de negócio contribui com registro de erros para o catálogo")]
    public void TodoModuloContribuiComRegistroDeErros()
    {
        HashSet<string> assembliesComRegistro =
        [
            .. RegistrosDeErroDaSolucao.Instanciar().Select(static r => r.GetType().Assembly.GetName().Name!),
        ];

        string[] semRegistro =
        [
            .. ModulosDoMonolito.Nomes
                .Select(static modulo => $"Unifesspa.UniPlus.{modulo}.API")
                .Where(api => !assembliesComRegistro.Contains(api)),
        ];

        semRegistro.Should().BeEmpty("a descoberta não achou registro de erros na API destes módulos, e os códigos deles ficariam fora do catálogo");
    }

    private static void RegerarSeSolicitado(string caminho, string gerado)
    {
        if (Environment.GetEnvironmentVariable(AtualizarArquivoEnvVar) == "1")
        {
            File.WriteAllText(caminho, gerado);
        }
    }

    private static string LerPublicado(string caminho)
    {
        File.Exists(caminho).Should().BeTrue($"o arquivo {ArquivoPublicado} não existe. {ComoRegerar([])}");

        try
        {
            string conteudo = File.ReadAllText(caminho).ReplaceLineEndings("\n");
            _ = DocumentoDoCatalogoDeErros.Ler(conteudo);
            return conteudo;
        }
        catch (JsonException erro)
        {
            throw new InvalidOperationException($"O arquivo {ArquivoPublicado} não é um JSON válido do catálogo ({erro.Message}). {ComoRegerar([])}", erro);
        }
    }

    private static string ComoRegerar(IReadOnlyList<string> divergencias) =>
        string.Join('\n', divergencias)
        + $"\nPara regerar: {AtualizarArquivoEnvVar}=1 dotnet test tests/Unifesspa.UniPlus.ArchTests "
        + "--filter \"FullyQualifiedName~CodigosDeErroPublicados\" e confira o diff de contracts/codigos-de-erro.json.";
}
