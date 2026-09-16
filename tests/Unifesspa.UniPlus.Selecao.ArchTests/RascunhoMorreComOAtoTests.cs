namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// O rascunho da publicação morre junto com o ato — e esta é a prova que não envelhece.
/// </summary>
/// <remarks>
/// <para>
/// O rascunho guarda o bloco que o operador transcreve do Diário Oficial, inclusive o nome de
/// quem assina. Registrado o ato, ele perde a razão de existir, e cada caminho que o registra
/// precisa apagá-lo — a raiz é soft-deletable, então o cascade da chave estrangeira jamais
/// dispara e nada acontece sozinho.
/// </para>
/// <para>
/// <b>Por que varredura e não lista.</b> Enumerar os comandos à mão funciona até o próximo
/// aparecer: foi assim que o atalho atômico de retificação — que registra ato sem passar por
/// sessão editorial nenhuma — passou despercebido no primeiro desenho. Derivar a lista de quem
/// declara <see cref="DadosDoAto"/> faz um caminho novo entrar na cobertura no dia em que
/// nasce, e falhar enquanto não apagar.
/// </para>
/// </remarks>
public sealed class RascunhoMorreComOAtoTests
{
    private static readonly Assembly Application = typeof(DadosDoAto).Assembly;

    public static TheoryData<Type> ComandosQueRegistramAto()
    {
        TheoryData<Type> dados = new();
        foreach (Type comando in Application.GetTypes().Where(DeclaraDadosDoAto).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            dados.Add(comando);
        }

        return dados;
    }

    [Fact(DisplayName = "A varredura encontra os comandos que registram ato — uma lista vazia provaria nada")]
    public void VarreduraNaoEstaVazia()
    {
        Application.GetTypes().Where(DeclaraDadosDoAto).Should().NotBeEmpty(
            "se nenhum comando declarasse os dados do ato, o teste abaixo passaria sem verificar caminho nenhum");
    }

    [Theory(DisplayName = "Todo comando que registra ato tem handler que apaga o rascunho da publicação")]
    [MemberData(nameof(ComandosQueRegistramAto))]
    public void HandlerApagaORascunho(Type comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        MethodInfo handle = HandleDoComando(comando);

        handle.GetParameters().Select(p => p.ParameterType)
            .Should().Contain(typeof(IRascunhoDePublicacaoRepository),
                $"{handle.DeclaringType!.Name} registra o ato de {comando.Name} e precisa apagar o rascunho — "
                + "nada apaga sozinho, porque ProcessoSeletivo é soft-deletable e o cascade nunca dispara");
    }

    [Theory(DisplayName = "O handler não só recebe o repositório do rascunho — ele chama a exclusão")]
    [MemberData(nameof(ComandosQueRegistramAto))]
    public void HandlerChamaAExclusao(Type comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // Injetar o repositório e não usá-lo passaria na verificação de assinatura acima e
        // deixaria o rascunho para trás do mesmo jeito. O que interessa é a chamada.
        string fonte = File.ReadAllText(CaminhoDoHandler(HandleDoComando(comando).DeclaringType!.Name));

        fonte.Should().Contain("ApagarDoProcessoAsync",
            $"o handler de {comando.Name} registra o ato e precisa apagar o rascunho da publicação");
    }

    private static IEnumerable<string> ArquivosDaApplication([CallerFilePath] string origem = "") =>
        Directory.EnumerateFiles(
            Path.GetFullPath(Path.Join(
                Path.GetDirectoryName(origem)!, "..", "..", "src", "selecao",
                "Unifesspa.UniPlus.Selecao.Application")),
            "*.cs",
            SearchOption.AllDirectories);

    private static string CaminhoDoHandler(string nomeDoHandler, [CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..", "..", "src", "selecao",
            "Unifesspa.UniPlus.Selecao.Application",
            "Commands", "ProcessosSeletivos", nomeDoHandler + ".cs"));

    [Fact(DisplayName = "Nenhum handler exclui processo sem antes apagar o rascunho da publicação")]
    public void ExcluirProcessoTeriaDeApagarORascunho()
    {
        // `IProcessoSeletivoRepository.Remover` faz soft delete (`MarkAsDeleted`), e por isso o
        // `ON DELETE CASCADE` da chave estrangeira do rascunho NUNCA dispara: nenhum `DELETE`
        // chega ao banco. Hoje não há handler que exclua processo, então o cenário não acontece
        // — mas o mecanismo já existe no repositório, e no dia em que alguém escrever a
        // exclusão, o rascunho sobreviveria com o nome de quem assinaria o ato dentro.
        //
        // Esta guarda existe para falhar NESSE dia, e não meses depois, quando o dado sobrevivente
        // já estiver fora de qualquer filtro e inalcançável por rota.
        string[] excluemProcesso = [.. ArquivosDaApplication()
            .Where(arquivo => Regex.IsMatch(
                File.ReadAllText(arquivo),
                @"processoSeletivoRepository\s*\.\s*Remover\s*\(",
                RegexOptions.IgnoreCase))
            .Select(arquivo => Path.GetFileName(arquivo)!)
            .OrderBy(nome => nome, StringComparer.Ordinal)];

        excluemProcesso.Should().BeEmpty(
            "excluir o processo apaga a linha? Não: a raiz é soft-deletable e o cascade não "
            + "dispara. Quem escrever a exclusão precisa chamar ApagarDoProcessoAsync do "
            + "rascunho da publicação no mesmo handler — e então acrescentar o arquivo à "
            + "cobertura desta guarda, com o teste que prova a exclusão");
    }

    private static bool DeclaraDadosDoAto(Type tipo) =>
        tipo.IsClass
        && tipo.Name.EndsWith("Command", StringComparison.Ordinal)
        && tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.PropertyType == typeof(DadosDoAto));

    private static MethodInfo HandleDoComando(Type comando)
    {
        MethodInfo? handle = Application.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m => m.Name == "Handle"
                && m.GetParameters().FirstOrDefault()?.ParameterType == comando);

        handle.Should().NotBeNull(
            $"{comando.Name} declara os dados do ato e precisa de um handler convention-based que o receba");
        return handle!;
    }
}
