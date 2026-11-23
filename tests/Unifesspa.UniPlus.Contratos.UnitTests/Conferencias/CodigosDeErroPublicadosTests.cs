namespace Unifesspa.UniPlus.Contratos.UnitTests.SolutionRules;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Contratos.UnitTests.Hosting;
using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;
using Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.TestSupport;


/// <summary>
/// O arquivo <c>contracts/codigos-de-erro.json</c> é a lista dos códigos de erro de fio que a API
/// emite, e é gerado dos registros de erro dos módulos e das definições da fronteira HTTP.
/// A correspondência com as descrições públicas é conferida no contrato de descrições.
/// Código novo, código removido, ou status e título alterados reprovam até o arquivo ser regerado.
/// </summary>
public sealed class CodigosDeErroPublicadosTests
{
    private const string ArquivoPublicado = "contracts/codigos-de-erro.json";

    private static readonly Lazy<DocumentoDoCatalogoDeErros> Gerado = new(() =>
        DocumentoDoCatalogoDeErros.Gerar(AgregadorDoCatalogoDeErros.Agregar(HostsDoProdutoIniciados.Executaveis.SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos.GetServices<IDomainErrorRegistration>()).DistinctBy(registro => registro.GetType()).ToArray())));

    [Fact(DisplayName = "O arquivo de códigos de erro bate com os códigos que a API emite")]
    public void ArquivoPublicadoBateComOsCodigosEmitidos()
    {
        string caminho = Path.Join(AppContext.BaseDirectory, ArquivoPublicado);
        string gerado = Gerado.Value.Serializar();

        string? publicado = File.Exists(caminho) ? File.ReadAllText(caminho).ReplaceLineEndings("\n") : null;
        if (!string.Equals(publicado, gerado, StringComparison.Ordinal))
        {
            string resultado = ResultadoDeConferencia.Gravar(Path.GetFileName(caminho), gerado);
            IReadOnlyList<string> divergencias = CompararPublicado(publicado);
            publicado.Should().Be(gerado, $"{string.Join('\n', divergencias)}\n{ResultadoDeConferencia.Mensagem(resultado, ArquivoPublicado)}");
        }
    }

    private static IReadOnlyList<string> CompararPublicado(string? publicado)
    {
        if (publicado is null)
            return ["O catálogo publicado está ausente."];
        try
        {
            return DivergenciasDoCatalogoDeErros.Comparar(DocumentoDoCatalogoDeErros.Ler(publicado).Codigos, Gerado.Value.Codigos);
        }
        catch (JsonException)
        {
            return ["O catálogo publicado contém JSON inválido ou campos fora do formato."];
        }
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
        [.. HostsDoProdutoIniciados.Executaveis.SelectMany(executavel => HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos.GetServices<IDomainErrorRegistration>()).DistinctBy(registro => registro.GetType()).ToArray().Select(static r => r.GetType().Assembly.GetName().Name!)];

    private static string[] AssembliesDeApiComController() =>
    [
        .. HostsDoProdutoIniciados.Executaveis.SelectMany(executavel =>
            HostsDoProdutoIniciados.Obter(executavel).ProvedorDeServicos.GetRequiredService<IActionDescriptorCollectionProvider>()
                .ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
            .Select(acao => acao.ControllerTypeInfo.Assembly.GetName().Name!)
            .Where(nome => nome.StartsWith("Unifesspa.UniPlus.", StringComparison.Ordinal))
            .Distinct(),
    ];

}
