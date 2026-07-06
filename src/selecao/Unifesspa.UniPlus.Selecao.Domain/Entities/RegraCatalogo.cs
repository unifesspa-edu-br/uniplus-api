namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Entrada da biblioteca <c>rol_de_regras</c> — uma regra TIPADA e VERSIONADA
/// que a configuração do Processo Seletivo referencia e o snapshot de
/// publicação congela por <c>(codigo, versao, hash)</c> (RN08). Materializa a
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
/// <see cref="EntityBase"/> puro (sem soft-delete) e a imutabilidade é imposta
/// no banco por gatilho <c>BEFORE UPDATE OR DELETE</c>; a própria linhagem de
/// versões é a trilha de auditoria (sem histórico forense adicional).
/// </para>
/// <para>
/// <strong>Hash content-addressable.</strong> O <see cref="Hash"/> é o SHA-256
/// canônico da definição COMPLETA (<c>codigo+versao+tipo+esquema_args+invariantes+base_legal</c>).
/// Mudar qualquer campo definicional mudaria o hash; como a versão é imutável,
/// o freeze do snapshot é reprodutível por construção.
/// </para>
/// <para>
/// <strong>Fronteira.</strong> A entrada descreve a regra e o
/// <c>esquema_args</c> (o <em>schema</em> dos argumentos que o admin preenche);
/// os <em>args aplicados</em> são tipados na dimensão que consome a regra
/// (<see cref="ReferenciaRegra"/> + payload próprio) e validados contra este
/// esquema. O <em>motor</em> que executa a regra (algoritmo de distribuição,
/// cálculo da nota) é incremento — consome a definição congelada, não é
/// modelado aqui.
/// </para>
/// </remarks>
public sealed class RegraCatalogo : EntityBase
{
    public string Codigo { get; private set; } = null!;
    public string Versao { get; private set; } = null!;
    public TipoRegra Tipo { get; private set; }

    /// <summary>Schema (jsonb, objeto) dos argumentos que o admin preenche ao aplicar a regra.</summary>
    public JsonElement EsquemaArgs { get; private set; }

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
        JsonElement esquemaArgs,
        JsonElement invariantes,
        string baseLegal)
    {
        Codigo = codigo;
        Versao = versao;
        Tipo = tipo;
        EsquemaArgs = esquemaArgs;
        Invariantes = invariantes;
        BaseLegal = baseLegal;
        Hash = ComputeHash();
    }

    /// <summary>
    /// Factory canônica (usada pelo seeder do catálogo): valida a definição,
    /// desanexa os payloads jsonb do <see cref="JsonDocument"/> de origem
    /// (<see cref="JsonElement.Clone"/>, para uma entidade imutável não segurar
    /// um documento externo) e computa o hash.
    /// </summary>
    public static Result<RegraCatalogo> Criar(
        string codigo,
        string versao,
        TipoRegra tipo,
        JsonElement esquemaArgs,
        JsonElement invariantes,
        string baseLegal)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.CodigoObrigatorio", "Código da regra é obrigatório."));
        }

        if (string.IsNullOrWhiteSpace(versao))
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.VersaoObrigatoria", "Versão da regra é obrigatória."));
        }

        if (tipo == TipoRegra.Nenhuma)
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.TipoObrigatorio", "Tipo da regra é obrigatório."));
        }

        if (esquemaArgs.ValueKind != JsonValueKind.Object)
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.EsquemaArgsInvalido",
                "esquema_args deve ser um objeto JSON (schema dos argumentos)."));
        }

        if (invariantes.ValueKind != JsonValueKind.Array)
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.InvariantesInvalidas",
                "invariantes deve ser um array JSON de pré/pós-condições."));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Result<RegraCatalogo>.Failure(new DomainError(
                "RegraCatalogo.BaseLegalObrigatoria",
                "Base legal é obrigatória (lei + artigo/§ ou decisão institucional)."));
        }

        return Result<RegraCatalogo>.Success(new RegraCatalogo(
            codigo.Trim(),
            versao.Trim(),
            tipo,
            esquemaArgs.Clone(),
            invariantes.Clone(),
            baseLegal.Trim()));
    }

    /// <summary>
    /// Recomputa o hash a partir do estado atual — exposto para os testes de
    /// determinismo/round-trip (recomputar deve bater com o <see cref="Hash"/>
    /// materializado).
    /// </summary>
    public string RecomputeHash() => ComputeHash();

    private string ComputeHash() => HashCanonicalComputer.ComputeRegraCatalogo(
        Codigo,
        Versao,
        Tipo,
        EsquemaArgs,
        Invariantes,
        BaseLegal);
}
