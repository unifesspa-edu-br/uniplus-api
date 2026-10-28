namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Collections.Frozen;
using System.Reflection;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Catálogo de um enum de permissões: lê o <see cref="PermissaoAttribute"/> de cada
/// membro, valida e entrega a <see cref="DefinicaoDePermissao"/> (ADR-0151).
/// </summary>
/// <typeparam name="TPermissao">Enum de permissões de um módulo.</typeparam>
/// <remarks>
/// A leitura acontece uma vez. Se o enum é inválido, toda consulta repete a mesma
/// <see cref="CatalogoDePermissoesInvalidoException"/>, de modo que o defeito derruba
/// a subida em vez de aparecer como um 403 plausível em execução.
/// </remarks>
public static partial class CatalogoDePermissoes<TPermissao>
    where TPermissao : struct, Enum
{
    private static readonly Lazy<FrozenDictionary<TPermissao, DefinicaoDePermissao>> Carregado =
        new(Carregar, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Módulo declarado no enum.</summary>
    public static string Modulo => ModuloDoEnum() ?? throw new CatalogoDePermissoesInvalidoException(
        [$"{typeof(TPermissao).Name}: falta [{nameof(ModuloDePermissoesAttribute)}] no enum."]);

    /// <summary>Confere o enum e devolve suas definições, em ordem de código.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O enum é inválido.</exception>
    public static IReadOnlyList<DefinicaoDePermissao> Todas()
        => [.. Carregado.Value.Values.OrderBy(static d => d.Codigo, StringComparer.Ordinal)];

    /// <summary>Definição de um membro.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O enum é inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é membro do enum, como num cast de inteiro.</exception>
    public static DefinicaoDePermissao Obter(TPermissao permissao)
        => Carregado.Value.TryGetValue(permissao, out DefinicaoDePermissao? definicao)
            ? definicao
            : throw new ArgumentOutOfRangeException(
                nameof(permissao),
                permissao,
                $"O valor '{permissao}' ({Convert.ToInt64(permissao, System.Globalization.CultureInfo.InvariantCulture)}) não é membro de {typeof(TPermissao).Name}.");

    private static string? ModuloDoEnum()
        => typeof(TPermissao).GetCustomAttribute<ModuloDePermissoesAttribute>()?.Modulo;

    private static FrozenDictionary<TPermissao, DefinicaoDePermissao> Carregar()
    {
        string tipo = typeof(TPermissao).Name;
        List<string> problemas = [];
        string? modulo = ModuloDoEnum();

        if (string.IsNullOrWhiteSpace(modulo))
        {
            problemas.Add($"{tipo}: falta [{nameof(ModuloDePermissoesAttribute)}] com o módulo do enum.");
        }

        Dictionary<TPermissao, DefinicaoDePermissao> definicoes = [];
        Dictionary<string, string> codigos = new(StringComparer.Ordinal);
        Dictionary<string, string> nomes = new(StringComparer.OrdinalIgnoreCase);

        foreach (FieldInfo campo in typeof(TPermissao).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            TPermissao membro = (TPermissao)campo.GetValue(null)!;
            string quem = $"{tipo}.{campo.Name}";

            PermissaoAttribute? atributo = campo.GetCustomAttribute<PermissaoAttribute>();

            if (atributo is null)
            {
                problemas.Add($"{quem}: falta [{nameof(PermissaoAttribute)}].");
                continue;
            }

            int antes = problemas.Count;
            ConferirTextos(quem, atributo, nomes, problemas);
            string[] segmentos = ConferirCodigo(quem, atributo.Codigo, modulo, codigos, problemas);
            ConferirEscopo(quem, atributo, problemas);

            Result<PermissionRequirement> requisito = PermissionRequirement.From(
                atributo.Codigo,
                atributo.Sensibilidade,
                atributo.BaseLegal,
                atributo.RequerMfa,
                atributo.RequerDuplaAprovacao,
                atributo.EscopoContextoObrigatorio,
                atributo.VerificacoesDeContexto);

            if (requisito.IsFailure)
            {
                problemas.Add($"{quem}: metadados recusados — {requisito.Error!.Message}");
            }

            if (problemas.Count == antes && segmentos.Length == 3)
            {
                definicoes[membro] = new DefinicaoDePermissao(
                    atributo.Codigo,
                    atributo.Nome,
                    atributo.Descricao,
                    segmentos[0],
                    segmentos[1],
                    segmentos[2],
                    requisito.Value!);
            }
        }

        if (problemas.Count > 0)
        {
            throw new CatalogoDePermissoesInvalidoException(problemas);
        }

        return definicoes.ToFrozenDictionary();
    }

    private static void ConferirTextos(
        string quem,
        PermissaoAttribute atributo,
        Dictionary<string, string> nomes,
        List<string> problemas)
    {
        if (string.IsNullOrWhiteSpace(atributo.Nome))
        {
            problemas.Add($"{quem}: o nome está vazio.");
        }
        else if (!nomes.TryAdd(atributo.Nome.Trim(), quem))
        {
            problemas.Add($"{quem}: o nome '{atributo.Nome}' já é de {nomes[atributo.Nome.Trim()]}.");
        }

        if (string.IsNullOrWhiteSpace(atributo.Descricao))
        {
            problemas.Add($"{quem}: a descrição está vazia.");
        }
    }

    private static string[] ConferirCodigo(
        string quem,
        string? codigo,
        string? modulo,
        Dictionary<string, string> codigos,
        List<string> problemas)
    {
        if (string.IsNullOrWhiteSpace(codigo) || !FormatoDoCodigo().IsMatch(codigo))
        {
            problemas.Add($"{quem}: o código '{codigo}' não segue {{módulo}}:{{recurso}}:{{ação}} em minúsculas, dígitos e hífen.");
            return [];
        }

        if (!codigos.TryAdd(codigo, quem))
        {
            problemas.Add($"{quem}: o código '{codigo}' já é de {codigos[codigo]}.");
        }

        string[] segmentos = codigo.Split(':');
        if (!string.IsNullOrWhiteSpace(modulo) && segmentos[0] != modulo)
        {
            problemas.Add($"{quem}: o código '{codigo}' tem o prefixo '{segmentos[0]}', mas o módulo do enum é '{modulo}'.");
        }

        return segmentos;
    }

    private static void ConferirEscopo(string quem, PermissaoAttribute atributo, List<string> problemas)
    {
        foreach (string campo in atributo.EscopoContextoObrigatorio ?? [])
        {
            if (!CamposDeContexto.NomesConhecidos.Contains(campo))
            {
                problemas.Add($"{quem}: o campo de contexto '{campo}' não é verificável pela decisão.");
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*:[a-z0-9]+(-[a-z0-9]+)*:[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoCodigo();
}
