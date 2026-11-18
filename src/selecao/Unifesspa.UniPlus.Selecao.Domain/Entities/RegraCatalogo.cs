namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Entrada da biblioteca <c>regras_do_catalogo</c> — uma regra TIPADA e VERSIONADA
/// que a configuração do Processo Seletivo referencia e o snapshot de
/// publicação congela por <c>(codigo, versao, hash)</c> (congelamento dos parâmetros na publicação, UNI-REQ-0019). Materializa a
/// tese "configurável, não programável": o comportamento (fórmula da nota,
/// distribuição de vagas, bônus, desempate, …) é conteúdo versionado de uma
/// regra, não constante do motor — lei muda → nova versão, não deploy.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Append-only, seed-governada.</strong> Não é CRUD de administrador:
/// as regras são semeadas (governadas por código) e a versão de uma regra é
/// <em>imutável</em> — corrigir/evoluir o comportamento é publicar uma nova
/// versão, nunca mutar a existente. Por isso a entidade deriva de
/// <see cref="EntityBase"/> puro (sem soft-delete). O append-only é imposto por
/// <em>convenção</em> — ausência de API de mutação, leitura por
/// <c>IRegraCatalogoReader</c> e fitness test —, não por gatilho de banco: não
/// há <c>trigger</c> sobre <c>regras_do_catalogo</c> em migration alguma. A própria
/// linhagem de versões é a trilha de auditoria (sem histórico forense adicional).
/// </para>
/// <para>
/// <strong>Fronteira da correção do seed (ADR-0112).</strong> Enquanto nenhuma
/// configuração congelada (<see cref="VersaoConfiguracao"/>) referenciar uma
/// linha por <c>(codigo, versao)</c>, essa linha ainda é vocabulário de seed e
/// pode ser corrigida por substituição. A partir do primeiro congelamento que a
/// referencia, ela vira fato e vale append-only estrito (congelamento dos parâmetros na publicação, UNI-REQ-0019) — o passado não
/// se muta. A verificação é mecânica, contra o conjunto de versões congeladas.
/// </para>
/// <para>
/// <strong>Hash content-addressable.</strong> O <see cref="Hash"/> é o SHA-256
/// canônico da definição COMPLETA (<c>codigo+versao+tipo+declaracao_da_regra+invariantes+base_legal</c>).
/// Mudar qualquer campo definicional mudaria o hash; como a versão é imutável,
/// o freeze do snapshot é reprodutível por construção.
/// </para>
/// <para>
/// A declaração contém somente o que a versão da regra declara de si. A forma dos
/// parâmetros aplicados é definida pelo esquema do documento da configuração.
/// </para>
/// </remarks>
public sealed class RegraCatalogo : EntityBase
{
    public string Codigo { get; private set; } = null!;
    public string Versao { get; private set; } = null!;
    public TipoRegra Tipo { get; private set; }

    /// <summary>
    /// O que a versão declara de si, como as naturezas de conceito a que uma regra de consolidação se aplica.
    /// </summary>
    public JsonElement DeclaracaoDaRegra { get; private set; }

    /// <summary>Pré/pós-condições declaradas da regra (jsonb, array).</summary>
    public JsonElement Invariantes { get; private set; }

    public string BaseLegal { get; private set; } = null!;

    /// <summary>SHA-256 canônico da definição completa (content-addressable).</summary>
    public string Hash { get; private set; } = null!;

    // Construtor de materialização do EF Core.
    private RegraCatalogo()
    {
    }

    private RegraCatalogo(
        string codigo,
        string versao,
        TipoRegra tipo,
        JsonElement declaracaoDaRegra,
        JsonElement invariantes,
        string baseLegal)
    {
        Codigo = codigo;
        Versao = versao;
        Tipo = tipo;
        DeclaracaoDaRegra = declaracaoDaRegra;
        Invariantes = invariantes;
        BaseLegal = baseLegal;
        Hash = HashCanonicalComputer.ComputeRegraCatalogo(this);
    }

    /// <summary>
    /// Factory canônica (usada pelo seeder do catálogo): confere a definição, desanexa os payloads
    /// jsonb do <see cref="JsonDocument"/> de origem (<see cref="JsonElement.Clone"/>, para uma
    /// entidade imutável não segurar um documento externo) e computa o hash.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A definição viola uma invariante: o catálogo é semeado por código, e definição inválida é
    /// defeito de quem semeia, não recusa a devolver a quem chama.
    /// </exception>
    public static RegraCatalogo Criar(
        string codigo,
        string versao,
        TipoRegra tipo,
        JsonElement declaracaoDaRegra,
        JsonElement invariantes,
        string baseLegal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLegal);
        ArgumentOutOfRangeException.ThrowIfEqual(tipo, TipoRegra.Nenhuma);
        ExigirForma(declaracaoDaRegra, JsonValueKind.Object, nameof(declaracaoDaRegra));
        ExigirForma(invariantes, JsonValueKind.Array, nameof(invariantes));

        return new RegraCatalogo(
            codigo.Trim(),
            versao.Trim(),
            tipo,
            declaracaoDaRegra.Clone(),
            invariantes.Clone(),
            baseLegal.Trim());
    }

    private static void ExigirForma(JsonElement valor, JsonValueKind esperada, string parametro)
    {
        if (valor.ValueKind != esperada)
        {
            throw new ArgumentException($"{parametro} deve ser {(esperada == JsonValueKind.Object ? "um objeto" : "um array")} JSON.", parametro);
        }
    }

    /// <summary>A identidade content-addressable desta regra, a que cada dimensão da configuração embute.</summary>
    public ReferenciaRegra Referencia() => ReferenciaRegra.De(Codigo, Versao, Hash);

    /// <summary>
    /// Recomputa o hash a partir do estado atual — exposto para os testes de
    /// determinismo/round-trip (recomputar deve bater com o <see cref="Hash"/>
    /// materializado).
    /// </summary>
    public string RecomputeHash() => HashCanonicalComputer.ComputeRegraCatalogo(this);
}
