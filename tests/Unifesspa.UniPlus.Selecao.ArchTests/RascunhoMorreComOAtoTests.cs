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

    [Theory(DisplayName = "O handler não emite a exclusão do rascunho antes do primeiro flush")]
    [MemberData(nameof(ComandosQueRegistramAto))]
    public void HandlerApagaDepoisDeGravar(Type comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // A exclusão é ExecuteDelete: SQL na hora, fora do rastreamento. Emitida antes do
        // flush, ela aposta que nada dali em diante recusa a operação — e quando alguma coisa
        // recusa, o operador recebe "nada foi publicado" com o bloco que transcreveu do Diário
        // Oficial já destruído, o do colega junto, porque a exclusão é por processo.
        // Ordem, aqui, é a diferença entre apagar o que perdeu a razão de existir e apagar o
        // que ainda vai ser preciso.
        // Sem os comentários: a guarda compara POSIÇÃO no texto, e prosa que cite a chamada
        // deslocaria a âncora para antes do flush de verdade — um handler que apagasse cedo
        // passaria, e a guarda diria o contrário. O que interessa é onde o CÓDIGO chama.
        string fonte = SemComentariosDeLinha(
            File.ReadAllText(CaminhoDoHandler(HandleDoComando(comando).DeclaringType!.Name)));

        // Âncora no PRIMEIRO flush, e é só isso que a guarda promete: a exclusão não vem antes
        // de qualquer gravação. Onde o handler tem flush intermediário — o descarte da
        // retificação repõe a configuração congelada antes de encerrar a sessão —, a ordem entre
        // a exclusão e o flush FINAL é decidida pelo desenho do handler, que ali proíbe qualquer
        // retorno de recusa depois do primeiro. Exigir flush explícito é deliberado: quem apaga
        // o rascunho precisa de um ponto no código onde já sabe que gravou, e não o `SaveChanges`
        // que o Wolverine dispara depois que o handler acabou.
        int primeiroFlush = fonte.IndexOf(".SalvarAlteracoesAsync(", StringComparison.Ordinal);
        primeiroFlush.Should().BeGreaterThan(-1,
            $"a exclusão do rascunho só é segura depois de um flush bem-sucedido, e o handler de "
            + $"{comando.Name} não tem nenhum");

        foreach (Match exclusao in Regex.Matches(fonte, @"\.ApagarDoProcessoAsync\(", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            exclusao.Index.Should().BeGreaterThan(primeiroFlush,
                $"o handler de {comando.Name} apaga o rascunho antes de gravar a versão — uma recusa "
                + "posterior devolveria 'nada foi publicado' com a transcrição do operador já apagada");
        }
    }

    /// <summary>
    /// Apaga os comentários <c>//</c> preservando o comprimento das linhas, para que os índices
    /// comparados continuem sendo os do arquivo original.
    /// </summary>
    private static string SemComentariosDeLinha(string fonte) =>
        Regex.Replace(fonte, @"//[^\r\n]*", m => new string(' ', m.Length), RegexOptions.None, TimeSpan.FromSeconds(5));

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
