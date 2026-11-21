namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using System.Reflection;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.ArchTests.TestSupport;
using Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// O arquivo <c>contracts/codigos-de-erro.json</c> é a lista dos códigos de erro de fio que a API
/// emite, e é gerado dos registros de erro dos módulos e das definições da fronteira HTTP.
/// A correspondência com as descrições públicas é conferida no contrato de descrições.
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

        foraDoPadrao.Should().BeEmpty("o código de fio é uniplus.<módulo>.<assunto>.<motivo>, e o portal agrupa os códigos a partir dessa estrutura");
    }

    [Fact(DisplayName = "Todo módulo cuja API expõe controller contribui com registro de erros para o catálogo")]
    public void TodoModuloComControllerContribuiComRegistroDeErros()
    {
        HashSet<string> comRegistro = AssembliesDeApiComRegistroDeErros();
        string[] comController = AssembliesDeApiComController();

        comController.Should().NotBeEmpty("sem nenhum controller a conferência não confere nada");
        comController.Where(api => !comRegistro.Contains(api)).Should().BeEmpty(
            "a descoberta não achou registro de erros na API destes módulos, e os códigos deles ficariam fora do catálogo");
    }

    private static HashSet<string> AssembliesDeApiComRegistroDeErros() =>
        [.. RegistrosDeErroDaSolucao.Instanciar().Select(static r => r.GetType().Assembly.GetName().Name!)];

    private static string[] AssembliesDeApiComController() =>
    [
        .. Classes().That().AreAssignableTo(typeof(ControllerBase)).And().AreNotAbstract()
            .GetObjects(ArquiteturaDasTravas.Produto)
            .Select(static c => c.Assembly.Name)
            .Where(static nome => nome.StartsWith("Unifesspa.UniPlus.", StringComparison.Ordinal))
            .Distinct(),
    ];

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
