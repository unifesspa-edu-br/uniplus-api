namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// A raiz do agregado coordena e guarda o estado; as regras de negócio vivem nas políticas de
/// <c>Domain.ProcessosSeletivos</c>. O critério é objetivo e lê o fonte da raiz: nenhum método passa de
/// 15 linhas, toda porta pública delega ao protocolo de edição ou a uma política, e nenhuma regra
/// constrói <c>DomainError</c> na raiz. O que foge do critério está listado por nome, com o limite
/// atual e o motivo, e não pode crescer.
/// </summary>
public sealed partial class RaizDoAgregadoTests
{
    private const int LimiteDeLinhas = 15;

    private static readonly Dictionary<string, (int Maximo, string Motivo)> MetodosMaisLongos = new()
    {
        ["AplicarModeloDeFormulario"] = (19, "protocolo próprio, sem precondição, que distingue a recusa de estado da recusa do plano"),
        ["Publicar"] = (26, "transição de status: confere os argumentos do pedido, o status e a prontidão, e emite a versão"),
        ["Retificar"] = (26, "transição de status: confere os argumentos do pedido, o status e a sessão aberta, e sucede a versão"),
        ["FecharRetificacao"] = (19, "transição que encerra a sessão só depois de a versão estar decidida"),
        ["DescartarRetificacao"] = (31, "encerra a sessão exigindo o carimbo da restauração da versão-base"),
        ["SucederVersao"] = (43, "núcleo comum da retificação atômica e do fechamento: status, motivo, cadeia de versões e prontidão"),
        ["PortoesDeCongelamento"] = (16, "monta os onze portões da prontidão, um delegado por portão"),
        ["RestaurarConfiguracaoCongelada"] = (36, "porta da restauração: status, cadeia de versões, validação do grafo e carimbo"),
        ["SombraParaVerificacao"] = (18, "cópia da identidade sem configuração; não há regra a delegar"),
        ["AbrirRetificacao"] = (40, "abre a sessão: status, cadeia de versões, sessão já aberta e criação do rascunho"),
        ["AlterarMotivoRetificacao"] = (30, "mutação da sessão com a allowlist inteira da guarda, e não o protocolo de edição da configuração"),
        ["EditarConferindo"] = (17, "o próprio protocolo de edição: guarda, conferência, aplicação e revisão"),
    };

    private static readonly Dictionary<string, string> MetodosQueConstroemDomainError = new()
    {
        ["Publicar"] = "erro da transição de status",
        ["Retificar"] = "erro da transição de status",
        ["SucederVersao"] = "erros da transição: status, motivo obrigatório e versão de outro processo",
        ["DescartarRetificacao"] = "erros da sessão: descarte sem restauração e com versão errada",
        ["RestaurarConfiguracaoCongelada"] = "erros da restauração: status e versão de outro processo",
        ["AbrirRetificacao"] = "erros da sessão: status e versão de outro processo",
    };

    private static readonly Dictionary<string, string> PortasSemPolitica = new()
    {
        ["EtapasDaFase"] = "consulta sobre a lista da própria raiz, sem regra",
        ["SombraParaVerificacao"] = "cópia da identidade sem configuração, sem regra",
        ["FormularioDe"] = "consulta que só lê a vista da configuração, sem regra",
        ["DerivacoesEfetivas"] = "consulta que só lê a vista da configuração, sem regra",
    };

    [Fact(DisplayName = "Nenhum método da raiz passa de 15 linhas, salvo os listados por nome e sem crescer")]
    public void MetodosDaRaiz_RespeitamOLimite()
    {
        IReadOnlyList<MetodoDaRaiz> metodos = LerMetodos();
        metodos.Count(static m => m.Publico).Should().BeGreaterThan(30, "a leitura do fonte tem de encontrar as portas da raiz");

        string[] acimaDoLimite = [.. metodos
            .Where(m => m.Linhas > LimiteDeLinhas && !MetodosMaisLongos.ContainsKey(m.Nome))
            .Select(m => $"{m.Nome} ({m.Linhas} linhas)")];
        acimaDoLimite.Should().BeEmpty("a regra de negócio vai para uma política; a raiz só coordena");

        string[] cresceram = [.. metodos
            .Where(m => MetodosMaisLongos.TryGetValue(m.Nome, out (int Maximo, string Motivo) limite) && m.Linhas > limite.Maximo)
            .Select(m => $"{m.Nome} ({m.Linhas} linhas, limite {MetodosMaisLongos[m.Nome].Maximo})")];
        cresceram.Should().BeEmpty("uma exceção nominal não pode crescer");
    }

    [Fact(DisplayName = "Toda porta pública da raiz delega ao protocolo de edição ou a uma política")]
    public void PortasPublicas_DelegamAoProtocoloOuAUmaPolitica()
    {
        string[] politicas = [.. NomesDasPoliticas()];

        string[] semDelegacao = [.. LerMetodos()
            .Where(m => m.Publico && !PortasSemPolitica.ContainsKey(m.Nome))
            .Where(m => !EditarRegex().IsMatch(m.Corpo) && !politicas.Any(p => Regex.IsMatch(m.Corpo, $@"\b{p}\b")))
            .Select(m => m.Nome)];

        semDelegacao.Should().BeEmpty("porta pública é Editar…(...) ou chamada de política, e não regra escrita na raiz");
    }

    [Fact(DisplayName = "A raiz só constrói DomainError nas transições e na sessão, nunca numa regra de configuração")]
    public void Raiz_NaoConstroiDomainErrorDeRegra()
    {
        string[] construtores = [.. LeitorDeIl.ChamadasPorMetodo(typeof(ProcessoSeletivo).Assembly)
            .Where(static c => c.Caller.FullName is { } nome && DonoDoMetodo(nome) == typeof(ProcessoSeletivo).FullName)
            .Where(static c => c.Callee is ConstructorInfo { DeclaringType.FullName: "Unifesspa.UniPlus.Kernel.Results.DomainError" })
            .Select(static c => MetodoDeOrigem(c.Metodo))
            .Where(static metodo => !MetodosQueConstroemDomainError.ContainsKey(metodo))
            .Distinct(StringComparer.Ordinal)];

        construtores.Should().BeEmpty("a recusa de uma regra pertence à política que a confere");
    }

    private static string DonoDoMetodo(string nomeCompleto)
    {
        int aninhado = nomeCompleto.IndexOf('+', StringComparison.Ordinal);
        return aninhado < 0 ? nomeCompleto : nomeCompleto[..aninhado];
    }

    /// <summary>O método escrito no fonte: a lambda ou a máquina de estado gerada é atribuída ao método que a declara.</summary>
    private static string MetodoDeOrigem(MethodBase metodo)
    {
        Match gerado = MetodoGeradoRegex().Match(metodo.Name);
        if (gerado.Success)
        {
            return gerado.Groups[1].Value;
        }

        Match tipoGerado = MetodoGeradoRegex().Match(metodo.DeclaringType!.Name);
        return tipoGerado.Success ? tipoGerado.Groups[1].Value : metodo.Name;
    }

    private static IEnumerable<string> NomesDasPoliticas() =>
        typeof(ProcessoSeletivo).Assembly.GetTypes()
            .Where(static t => t.Namespace is { } ns && ns.StartsWith("Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos", StringComparison.Ordinal))
            .Where(static t => !t.IsNested && !Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute)))
            .Select(static t => t.Name)
            .Where(static nome => nome is not ("EstadoDaConfiguracao" or "ColecoesDoProcesso" or "PartesDoProcesso"));

    private static List<MetodoDaRaiz> LerMetodos()
    {
        string[] linhas = File.ReadAllLines(CaminhoDaRaiz());
        List<MetodoDaRaiz> metodos = [];
        for (int i = 0; i < linhas.Length; i++)
        {
            if (!DeclaracaoDeMembroRegex().IsMatch(linhas[i]))
            {
                continue;
            }

            int fim = FimDoMembro(linhas, i);
            string declaracao = linhas[i].Split("=>")[0].Split('{')[0];
            if (declaracao.Contains('(', StringComparison.Ordinal))
            {
                Match nome = NomeDoMetodoRegex().Match(declaracao);
                metodos.Add(new MetodoDaRaiz(
                    nome.Groups[1].Value,
                    linhas[i].TrimStart().StartsWith("public ", StringComparison.Ordinal),
                    fim - i + 1,
                    string.Join('\n', linhas[i..(fim + 1)])));
            }

            i = fim;
        }

        return metodos;
    }

    private static int FimDoMembro(string[] linhas, int inicio)
    {
        int saldo = 0;
        bool comCorpo = false;
        for (int j = inicio; j < linhas.Length; j++)
        {
            string codigo = LiteralRegex().Replace(linhas[j], "\"\"");
            saldo += codigo.Count(static c => c is '{' or '(') - codigo.Count(static c => c is '}' or ')');
            comCorpo |= codigo == "    {";
            if (saldo <= 0 && (codigo.TrimEnd().EndsWith(';') || (comCorpo && codigo == "    }")))
            {
                return j;
            }
        }

        return linhas.Length - 1;
    }

    private static string CaminhoDaRaiz([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!, "..", "..",
            "src", "selecao", "Unifesspa.UniPlus.Selecao.Domain", "Entities", "ProcessoSeletivo.cs"));

    [GeneratedRegex(@"^    (public|internal|private|protected)\s")]
    private static partial Regex DeclaracaoDeMembroRegex();

    [GeneratedRegex(@"(\w+)\s*(<[^>(]*>)?\(")]
    private static partial Regex NomeDoMetodoRegex();

    [GeneratedRegex("\"(?:[^\"\\\\]|\\\\.)*\"")]
    private static partial Regex LiteralRegex();

    [GeneratedRegex(@"\bEditar(Conferindo)?(<\w+>)?\(")]
    private static partial Regex EditarRegex();

    [GeneratedRegex(@"^<(\w+)>")]
    private static partial Regex MetodoGeradoRegex();

    private sealed record MetodoDaRaiz(string Nome, bool Publico, int Linhas, string Corpo);
}
