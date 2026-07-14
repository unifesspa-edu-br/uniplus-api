namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

/// <summary>
/// <b>Todo handler que muta o <c>ProcessoSeletivo</c> carrega o agregado por
/// <c>ObterParaMutacaoAsync</c></b> (ADR-0110 D4).
/// </summary>
/// <remarks>
/// <para>
/// A allowlist da D4 lê <c>Rascunho is not null</c> para decidir se um certame publicado
/// aceita mutação. Mas <c>Rascunho</c> nulo é <b>ambíguo</b>: significa "não existe"
/// <b>e</b> "não foi carregado". Um handler que use <c>ObterComConfiguracaoAsync</c> — que
/// não traz a navegação — veria "sem sessão" num processo que tem uma, e recusaria a edição
/// legítima com <c>MutacaoPosPublicacaoBloqueada</c>: <b>fail-closed indevido</b>, e
/// silencioso, porque a mensagem de erro seria plausível.
/// </para>
/// <para>
/// A ADR escolheu fechar essa porta com um carregamento explícito <b>mais este teste</b> —
/// o método sozinho não impede ninguém de chamar o outro. É a mesma mecânica de
/// <see cref="RestauracaoSempreProvadaTests"/>: um fitness que prova que todo caller passa
/// pela porta certa.
/// </para>
/// </remarks>
public sealed class CarregamentoDeMutacaoTests
{
    private const string CarregamentoDeMutacao = "ObterParaMutacaoAsync";
    private const string CarregamentoDeLeitura = "ObterComConfiguracaoAsync";

    /// <summary>
    /// Onde <c>ObterComConfiguracaoAsync</c> pode aparecer legitimamente: a declaração, a
    /// implementação, e os <b>handlers de QUERY</b> — que só leem, não decidem sobre
    /// mutação, e para os quais o lock pessimista seria puro dano (duas consultas
    /// concorrentes ao mesmo processo se serializariam sem escrever nada).
    /// </summary>
    private static readonly string[] LeitoresAutorizados =
    [
        "Unifesspa.UniPlus.Selecao.Domain/Interfaces/IProcessoSeletivoRepository.cs",
        "Unifesspa.UniPlus.Selecao.Infrastructure/Persistence/Repositories/ProcessoSeletivoRepository.cs",
        "Unifesspa.UniPlus.Selecao.Infrastructure/Canonicalization/SnapshotPublicacaoCanonicalizer.cs",
        "Unifesspa.UniPlus.Selecao.Application/Queries/ProcessosSeletivos/ObterProcessoSeletivoQueryHandler.cs",
        "Unifesspa.UniPlus.Selecao.Application/Queries/ProcessosSeletivos/ObterConformidadeProcessoSeletivoQueryHandler.cs",
    ];

    [Fact(DisplayName = "Nenhum handler de COMANDO carrega o processo pelo caminho de leitura — a sessão editorial não seria vista")]
    public void HandlersDeComando_NaoUsamCarregamentoDeLeitura()
    {
        Regex leitura = new($@"\b{CarregamentoDeLeitura}\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        List<string> infratores = [];

        foreach (string arquivo in ArquivosDeProducao())
        {
            if (LeitoresAutorizados.Any(a => Normalizar(arquivo).EndsWith(a, StringComparison.Ordinal)))
            {
                continue;
            }

            if (leitura.IsMatch(CodigoSemComentarios(arquivo)))
            {
                infratores.Add(Path.GetRelativePath(RaizDoSrc(), arquivo));
            }
        }

        infratores.Should().BeEmpty(
            $"'{CarregamentoDeLeitura}' NÃO traz o RascunhoRetificacao. Um handler de mutação que o use lê " +
            "'Rascunho == null' como 'não há sessão editorial' quando ela apenas não foi carregada — e recusa " +
            "uma edição legítima com uma mensagem plausível ('utilize a retificação'), que ninguém vai " +
            $"desconfiar. Para MUTAR, é '{CarregamentoDeMutacao}' (ADR-0110 D4), que carrega a sessão e trava a " +
            $"linha raiz. Infratores: {string.Join(", ", infratores)}");
    }

    [Fact(DisplayName = "Todo handler de comando de ProcessoSeletivo que carrega o agregado usa o carregamento de mutação")]
    public void HandlersDeComando_UsamCarregamentoDeMutacao()
    {
        string pastaDeComandos = Path.Join(
            RaizDoSrc(), "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos");

        Directory.Exists(pastaDeComandos).Should().BeTrue();

        // Só interessam os handlers que MATERIALIZAM a raiz para decidir sobre ela — os que
        // a leem de volta do banco. O handler de criação, por exemplo, também recebe o
        // repositório, mas para `AdicionarAsync`: ele constrói o agregado do zero, não tem
        // estado anterior a consultar, e portanto não tem allowlist a aplicar.
        Regex carregaARaiz = new(
            @"processoSeletivoRepository\s*(//[^\n]*\n\s*)*\.\s*Obter\w*Async\s*\(",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        List<string> semCarregamentoDeMutacao = [];
        List<string> inspecionados = [];

        foreach (string arquivo in Directory.EnumerateFiles(pastaDeComandos, "*CommandHandler.cs"))
        {
            string codigo = CodigoSemComentarios(arquivo);

            if (!carregaARaiz.IsMatch(codigo))
            {
                continue;
            }

            inspecionados.Add(Path.GetFileName(arquivo));

            if (!codigo.Contains($".{CarregamentoDeMutacao}(", StringComparison.Ordinal))
            {
                semCarregamentoDeMutacao.Add(Path.GetFileName(arquivo));
            }
        }

        // Sem esta asserção, um regex que deixasse de casar tornaria o teste vácuo: zero
        // handlers inspecionados, zero infratores, verde.
        inspecionados.Should().NotBeEmpty("o detector precisa estar encontrando os handlers que carregam a raiz");

        semCarregamentoDeMutacao.Should().BeEmpty(
            "todo handler de comando que carrega o ProcessoSeletivo precisa fazê-lo por " +
            $"'{CarregamentoDeMutacao}' — é ele que traz a sessão editorial (sem a qual a allowlist da D4 " +
            "decide errado) e que trava a linha raiz (sem a qual uma publicação concorrente furaria a RN08). " +
            $"Handlers sem ele: {string.Join(", ", semCarregamentoDeMutacao)}");
    }

    /// <summary>
    /// O detector não é vácuo: ele encontra o carregamento de mutação onde ele deve estar. Um
    /// regex que não casasse com nada faria os dois testes acima passarem protegendo nada.
    /// </summary>
    [Fact(DisplayName = "O detector funciona — encontra o carregamento de mutação nos handlers que o usam")]
    public void Detector_EncontraOCarregamentoDeMutacao()
    {
        string definirEtapas = Path.Join(
            RaizDoSrc(), "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos",
            "DefinirEtapasCommandHandler.cs");

        File.Exists(definirEtapas).Should().BeTrue();
        File.ReadAllText(definirEtapas).Should().Contain($".{CarregamentoDeMutacao}(");
    }

    /// <summary>
    /// E o carregamento de mutação faz o que promete: traz a sessão editorial e trava a
    /// linha. Um <c>ObterParaMutacaoAsync</c> que esquecesse o <c>Include(Rascunho)</c>
    /// passaria nos dois fitness acima e recriaria, por dentro, exatamente o defeito que eles
    /// existem para impedir.
    /// </summary>
    [Fact(DisplayName = "O carregamento de mutação carrega a sessão editorial E trava a linha raiz")]
    public void CarregamentoDeMutacao_TrazSessaoETrava()
    {
        string repositorio = Path.Join(
            RaizDoSrc(), "selecao", "Unifesspa.UniPlus.Selecao.Infrastructure", "Persistence", "Repositories",
            "ProcessoSeletivoRepository.cs");

        string fonte = File.ReadAllText(repositorio);
        int inicio = fonte.IndexOf($"public async Task<ProcessoSeletivo?> {CarregamentoDeMutacao}(", StringComparison.Ordinal);
        inicio.Should().BeGreaterThan(0, "o método precisa existir para ser inspecionado");

        // Da assinatura até o fim do método (o próximo `public`/`private` de mesma indentação).
        int fim = fonte.IndexOf("\n    private ", inicio, StringComparison.Ordinal);
        string corpo = fim > inicio ? fonte[inicio..fim] : fonte[inicio..];

        corpo.Should().Contain("FOR UPDATE",
            "sem o lock pessimista, um Definir* que leu Status=Rascunho antes de uma publicação concorrente " +
            "persistiria a mutação DEPOIS de a versão já ter sido congelada — furando a RN08");
        corpo.Should().Contain("Include(p => p.Rascunho)",
            "sem a sessão editorial carregada, a allowlist da D4 lê 'não foi carregado' como 'não existe' e " +
            "recusa a edição legítima de um certame em retificação");
    }

    /// <summary>
    /// <b>Ninguém muta uma etapa por fora do agregado.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>EtapaProcesso.AtualizarDados</c> é público (o handler precisa dele para reconciliar
    /// por <c>Id</c>) e <c>ProcessoSeletivo.Etapas</c> expõe as instâncias. Nada, na
    /// linguagem, impede <c>processo.Etapas.First().AtualizarDados(...)</c> seguido de um
    /// <c>SaveChanges</c>: a configuração muda, o guard não roda, e — o que é pior desde a
    /// ADR-0110 — a <b>revisão não incrementa</b>. O ETag que o outro administrador tem em
    /// mãos continua válido depois de a configuração ter mudado debaixo dele, que é
    /// exatamente a edição cega que a sessão editorial existe para impedir.
    /// </para>
    /// <para>
    /// A porta não dá para fechar no tipo (o handler é outro assembly), então é fechada aqui.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "Só o agregado e o handler de etapas mutam uma EtapaProcesso — mutar por fora não incrementaria a revisão")]
    public void AtualizarDadosDeEtapa_SoPelosCallersAutorizados()
    {
        string[] autorizados =
        [
            // A declaração.
            "Unifesspa.UniPlus.Selecao.Domain/Entities/EtapaProcesso.cs",

            // O agregado, ao repor a configuração congelada (reconciliação por Id — D2).
            "Unifesspa.UniPlus.Selecao.Domain/Entities/ProcessoSeletivo.cs",

            // O handler do PUT /etapas, que reconcilia por Id para preservar o etapa_ref —
            // e que só chega até aqui DEPOIS de MutacaoBloqueada ter passado.
            "Unifesspa.UniPlus.Selecao.Application/Commands/ProcessosSeletivos/DefinirEtapasCommandHandler.cs",
        ];

        Regex mutacao = new(@"\bAtualizarDados\b", RegexOptions.None, TimeSpan.FromSeconds(5));

        List<string> infratores = [.. ArquivosDeProducao()
            .Where(a => !autorizados.Any(x => Normalizar(a).EndsWith(x, StringComparison.Ordinal)))
            .Where(a => mutacao.IsMatch(CodigoSemComentarios(a)))
            .Select(a => Path.GetRelativePath(RaizDoSrc(), a))];

        infratores.Should().BeEmpty(
            "mutar uma etapa por fora do agregado altera a configuração sem passar pelo guard e sem incrementar a " +
            "revisão da sessão editorial — o ETag do outro administrador continuaria válido depois de a " +
            $"configuração ter mudado debaixo dele (ADR-0110 D5). Infratores: {string.Join(", ", infratores)}");
    }

    /// <summary>
    /// <b>Quem descarta a sessão, restaura antes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este é o buraco mais perigoso da Feature inteira, e ele é <b>silencioso</b>. Enquanto a
    /// sessão editorial existe, os seis <c>Definir*</c> escrevem <b>direto na configuração
    /// viva</b> — não há staging, e todo <c>Definir*</c> substitui a coleção inteira. Um
    /// descarte que apenas <b>encerrasse</b> a sessão, sem repor, devolveria o certame ao
    /// estado "publicado normal" servindo uma configuração que <b>nunca foi publicada</b> e
    /// que diverge do documento que o publicou. Ninguém acusaria: o status estaria certo, a
    /// versão congelada estaria intacta, e só a configuração viva estaria mentindo.
    /// </para>
    /// <para>
    /// O <b>fechamento</b> é o oposto — ele encerra a sessão <b>de propósito sem repor</b>,
    /// porque é justamente a configuração editada que ele congela na versão N+1. Por isso os
    /// dois são autorizados, e por razões contrárias.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "Quem DESCARTA a sessão repõe a configuração congelada antes — encerrar sem repor deixaria o certame servindo o que nunca foi publicado")]
    public void Descarte_RestauraAntesDeEncerrarASessao()
    {
        string handler = Path.Join(
            RaizDoSrc(), "selecao", "Unifesspa.UniPlus.Selecao.Application", "Commands", "ProcessosSeletivos",
            "DescartarRetificacaoCommandHandler.cs");

        File.Exists(handler).Should().BeTrue();
        string fonte = File.ReadAllText(handler);

        fonte.Should().Contain("restaurador.Restaurar(",
            "sem a reposição, o descarte não desfaz nada — ele só remove a trava e deixa a configuração alterada de pé");

        int restaura = fonte.IndexOf("restaurador.Restaurar(", StringComparison.Ordinal);
        int encerra = fonte.IndexOf(".DescartarRetificacao(", StringComparison.Ordinal);

        encerra.Should().BeGreaterThan(0, "o handler precisa encerrar a sessão");
        restaura.Should().BeLessThan(
            encerra,
            "a reposição vem ANTES de encerrar a sessão: se ela falhar (round-trip divergente), o Failure sai com o " +
            "rascunho ainda aberto e nada é salvo — a sessão sobrevive para ser tentada de novo. Invertida, uma prova " +
            "que falhasse já teria destruído a sessão");
    }

    [Fact(DisplayName = "Só o descarte e o fechamento encerram a sessão editorial")]
    public void EncerrarSessao_SoPelosCallersAutorizados()
    {
        string[] autorizados =
        [
            // A declaração, no agregado.
            "Unifesspa.UniPlus.Selecao.Domain/Entities/ProcessoSeletivo.cs",

            // Descartar: restaura a configuração congelada e encerra.
            "Unifesspa.UniPlus.Selecao.Application/Commands/ProcessosSeletivos/DescartarRetificacaoCommandHandler.cs",
        ];

        // A CHAMADA de instância — `processo.DescartarRetificacao(...)`. O identificador nu
        // apareceria também no nome da action do controller e no do command, que não encerram
        // sessão nenhuma: são o transporte. O ponto cego (capturar o método como method group)
        // é aceitável aqui, porque o outro fitness — o que exige a restauração ANTES — já
        // guarda o único caller legítimo.
        Regex descarte = new(@"\.DescartarRetificacao\(", RegexOptions.None, TimeSpan.FromSeconds(5));

        List<string> infratores = [.. ArquivosDeProducao()
            .Where(a => !autorizados.Any(x => Normalizar(a).EndsWith(x, StringComparison.Ordinal)))
            .Where(a => descarte.IsMatch(CodigoSemComentarios(a)))
            .Select(a => Path.GetRelativePath(RaizDoSrc(), a))];

        infratores.Should().BeEmpty(
            "encerrar a sessão sem repor a configuração congelada devolve o certame ao estado 'publicado normal' " +
            "servindo, em silêncio, uma configuração que nunca foi publicada. O único caminho que encerra SEM repor é " +
            $"o FECHAMENTO — e ele o faz de propósito, porque congela a edição. Infratores: {string.Join(", ", infratores)}");
    }

    private static IEnumerable<string> ArquivosDeProducao() =>
        Directory.EnumerateFiles(RaizDoSrc(), "*.cs", SearchOption.AllDirectories)
            .Where(static a =>
                !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Normalizar(string caminho) => caminho.Replace('\\', '/');

    private static string CodigoSemComentarios(string arquivo) => string.Join(
        '\n',
        File.ReadLines(arquivo).Where(static linha => !linha.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    private static string RaizDoSrc([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", "..", "src"));
}
