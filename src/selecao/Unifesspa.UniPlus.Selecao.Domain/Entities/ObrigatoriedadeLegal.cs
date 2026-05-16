namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Regra legal data-driven que valida invariantes de um <see cref="Edital"/>
/// antes da publicação (ADR-0058). Carrega citação legal, vigência temporal,
/// hash canônico e governança por área (ADR-0057) para suportar evidência
/// forense em mandados de segurança e processos administrativos.
/// </summary>
/// <remarks>
/// <para>
/// A entidade encapsula o estado e os invariantes do agregado. A
/// validação e normalização do payload de entrada é responsabilidade do
/// <see cref="ObrigatoriedadeLegalPayloadNormalizer"/> (Domain Service),
/// invocado pelas factories — separação de responsabilidades alinhada
/// com SRP.
/// </para>
/// <para>
/// A propriedade <see cref="AreasDeInteresse"/> reflete o snapshot in-memory
/// do conjunto de áreas que enxergam a regra. A verdade persistida vive na
/// junction temporal <c>obrigatoriedade_legal_areas_de_interesse</c>
/// (ADR-0060), reconciliada pelo repositório/admin CRUD que entra em #461.
/// Em V1 esta entidade é hidratada via factory (admin POST) e o repositório
/// é responsável por traduzir o set para INSERTs na junction; queries de
/// visibilidade leem a junction, não esta propriedade.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1056:URI-like properties should not be strings",
    Justification = "AtoNormativoUrl é payload textual exibido para auditoria — pode incluir DOI, "
        + "URN, IRI ou identificadores não-HTTP que System.Uri suporta apenas com workarounds. "
        + "Mantemos string para preservar fidelidade do valor original informado pelo admin.")]
[SuppressMessage(
    "Design",
    "CA1054:URI-like parameters should not be strings",
    Justification = "Pareado com a justificativa de CA1056 acima — factory aceita string para "
        + "preservar fidelidade do payload textual da citação normativa.")]
public sealed class ObrigatoriedadeLegal : EntityBase, IAuditableEntity, IAreaScopedEntity
{
    /// <summary>
    /// Valor sentinela aceito em <see cref="TipoEditalCodigo"/> para regras
    /// universais (aplicam-se a qualquer tipo de edital). Alinha com a chave
    /// de filtro pública <c>?tipoEdital=*</c> exposta em #461.
    /// </summary>
    public const string TipoEditalUniversal = "*";

    public string TipoEditalCodigo { get; private set; } = null!;
    public CategoriaObrigatoriedade Categoria { get; private set; }
    public string RegraCodigo { get; private set; } = null!;
    public PredicadoObrigatoriedade Predicado { get; private set; } = null!;
    public string DescricaoHumana { get; private set; } = null!;
    public string BaseLegal { get; private set; } = null!;
    public string? AtoNormativoUrl { get; private set; }
    public string? PortariaInternaCodigo { get; private set; }
    public DateOnly VigenciaInicio { get; private set; }
    public DateOnly? VigenciaFim { get; private set; }

    /// <summary>
    /// Hash SHA-256 canônico do conteúdo semântico (CA-05). Recomputado pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> antes do <c>SaveChanges</c>
    /// para garantir que mutações via reflection ou property hidratada
    /// fora-de-factory ainda assim resultem em hash correto.
    /// </summary>
    public string Hash { get; private set; } = null!;

    public AreaCodigo? Proprietario { get; private set; }

    private HashSet<AreaCodigo> _areasDeInteresse = [];

    /// <summary>
    /// Snapshot in-memory do conjunto de áreas de interesse. A verdade
    /// temporal mora na junction <c>obrigatoriedade_legal_areas_de_interesse</c>
    /// (ADR-0060) — esta propriedade é a forma de domínio que aplica a
    /// Invariante 1 do ADR-0057 (Proprietario ∈ AreasDeInteresse).
    /// </summary>
    public IReadOnlySet<AreaCodigo> AreasDeInteresse => _areasDeInteresse;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Construtor de materialização do EF Core.
    private ObrigatoriedadeLegal()
    {
    }

    private ObrigatoriedadeLegal(NormalizedPayload payload, PredicadoObrigatoriedade predicado)
    {
        TipoEditalCodigo = payload.TipoEditalCodigo;
        Categoria = payload.Categoria;
        RegraCodigo = payload.RegraCodigo;
        Predicado = predicado;
        DescricaoHumana = payload.DescricaoHumana;
        BaseLegal = payload.BaseLegal;
        AtoNormativoUrl = payload.AtoNormativoUrl;
        PortariaInternaCodigo = payload.PortariaInternaCodigo;
        VigenciaInicio = payload.VigenciaInicio;
        VigenciaFim = payload.VigenciaFim;
        Proprietario = payload.Proprietario;
        _areasDeInteresse = [.. payload.AreasDeInteresse];
        Hash = ComputeHash();
    }

    /// <summary>
    /// Factory canônica (Story #460) — devolve uma regra na forma plena com
    /// hash já computado e payload normalizado pelo
    /// <see cref="ObrigatoriedadeLegalPayloadNormalizer"/>.
    /// </summary>
    public static Result<ObrigatoriedadeLegal> Criar(
        string tipoEditalCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string descricaoHumana,
        string baseLegal,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim = null,
        string? atoNormativoUrl = null,
        string? portariaInternaCodigo = null,
        AreaCodigo? proprietario = null,
        IReadOnlySet<AreaCodigo>? areasDeInteresse = null)
    {
        if (predicado is null)
        {
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.PredicadoObrigatorio",
                "Predicado é obrigatório."));
        }

        Result<NormalizedPayload> normalized = ObrigatoriedadeLegalPayloadNormalizer.Normalizar(
            tipoEditalCodigo,
            categoria,
            regraCodigo,
            descricaoHumana,
            baseLegal,
            atoNormativoUrl,
            portariaInternaCodigo,
            vigenciaInicio,
            vigenciaFim,
            proprietario,
            areasDeInteresse);

        return normalized.IsFailure
            ? Result<ObrigatoriedadeLegal>.Failure(normalized.Error!)
            : Result<ObrigatoriedadeLegal>.Success(new ObrigatoriedadeLegal(normalized.Value!, predicado));
    }

    /// <summary>
    /// Factory de retrocompatibilidade preservada para os testes do avaliador
    /// (#459): aplica defaults pragmáticos para os campos novos da forma
    /// plena (universal, categoria <see cref="CategoriaObrigatoriedade.Outros"/>,
    /// vigência aberta a partir de "hoje" do <paramref name="clock"/>, global).
    /// Use a sobrecarga completa em código de produção.
    /// </summary>
    /// <param name="clock">
    /// Fonte de "hoje" para <c>VigenciaInicio</c>. Default
    /// <see cref="TimeProvider.System"/>; testes determinísticos passam
    /// um <see cref="TimeProvider"/> fake para isolar o cenário do relógio
    /// do sistema.
    /// </param>
    public static Result<ObrigatoriedadeLegal> Criar(
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string baseLegal,
        string descricaoHumana,
        string? portariaInternaCodigo = null,
        TimeProvider? clock = null)
    {
        TimeProvider effectiveClock = clock ?? TimeProvider.System;
        return Criar(
            tipoEditalCodigo: TipoEditalUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: descricaoHumana,
            baseLegal: baseLegal,
            vigenciaInicio: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime.Date),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: portariaInternaCodigo,
            proprietario: null,
            areasDeInteresse: null);
    }

    /// <summary>
    /// Atualiza os campos semânticos da regra (Story #461 admin PUT) e
    /// recomputa o hash. O caller é responsável por respeitar a política
    /// de versionamento documentada no ADR-0058 (soft-delete + new row vs
    /// in-place update); esta entidade só garante consistência interna.
    /// </summary>
    public Result Atualizar(
        string tipoEditalCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string descricaoHumana,
        string baseLegal,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim = null,
        string? atoNormativoUrl = null,
        string? portariaInternaCodigo = null,
        AreaCodigo? proprietario = null,
        IReadOnlySet<AreaCodigo>? areasDeInteresse = null)
    {
        if (predicado is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.PredicadoObrigatorio",
                "Predicado é obrigatório."));
        }

        Result<NormalizedPayload> normalized = ObrigatoriedadeLegalPayloadNormalizer.Normalizar(
            tipoEditalCodigo,
            categoria,
            regraCodigo,
            descricaoHumana,
            baseLegal,
            atoNormativoUrl,
            portariaInternaCodigo,
            vigenciaInicio,
            vigenciaFim,
            proprietario,
            areasDeInteresse);

        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error!);
        }

        AplicarPayload(normalized.Value!, predicado);
        return Result.Success();
    }

    private void AplicarPayload(NormalizedPayload payload, PredicadoObrigatoriedade predicado)
    {
        TipoEditalCodigo = payload.TipoEditalCodigo;
        Categoria = payload.Categoria;
        RegraCodigo = payload.RegraCodigo;
        Predicado = predicado;
        DescricaoHumana = payload.DescricaoHumana;
        BaseLegal = payload.BaseLegal;
        AtoNormativoUrl = payload.AtoNormativoUrl;
        PortariaInternaCodigo = payload.PortariaInternaCodigo;
        VigenciaInicio = payload.VigenciaInicio;
        VigenciaFim = payload.VigenciaFim;
        Proprietario = payload.Proprietario;
        _areasDeInteresse = [.. payload.AreasDeInteresse];
        Hash = ComputeHash();
    }

    /// <summary>
    /// Recomputa o hash a partir do estado atual da entidade — invocado pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> antes do save para
    /// proteger contra mutações via reflection/EF property hydration que
    /// bypassem <see cref="Criar"/> e <see cref="Atualizar"/>.
    /// </summary>
    public string RecomputeHash()
    {
        Hash = ComputeHash();
        return Hash;
    }

    private string ComputeHash() => HashCanonicalComputer.Compute(
        TipoEditalCodigo,
        Categoria,
        RegraCodigo,
        Predicado,
        BaseLegal,
        PortariaInternaCodigo,
        VigenciaInicio,
        VigenciaFim);
}
