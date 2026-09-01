namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Regra legal data-driven aplicável a um <see cref="ProcessoSeletivo"/>
/// antes da publicação (ADR-0058). Carrega citação legal, vigência temporal e
/// hash canônico para suportar evidência forense em mandados de segurança e
/// processos administrativos.
/// </summary>
/// <remarks>
/// A entidade encapsula o estado e os invariantes do agregado. A
/// validação e normalização do payload de entrada é responsabilidade do
/// <see cref="ObrigatoriedadeLegalPayloadNormalizer"/> (Domain Service),
/// invocado pelas factories — separação de responsabilidades alinhada
/// com SRP.
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
public sealed class ObrigatoriedadeLegal : SoftDeletableEntity, IAuditableEntity
{
    /// <summary>
    /// Valor sentinela aceito em <see cref="TipoProcessoCodigo"/> para regras
    /// universais (aplicam-se a qualquer tipo de processo). Alinha com a chave
    /// de filtro pública <c>?tipoProcesso=*</c>.
    /// </summary>
    public const string TipoProcessoUniversal = "*";

    public string TipoProcessoCodigo { get; private set; } = null!;
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

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Construtor de materialização do EF Core.
    private ObrigatoriedadeLegal()
    {
    }

    private ObrigatoriedadeLegal(NormalizedPayload payload, PredicadoObrigatoriedade predicado)
    {
        TipoProcessoCodigo = payload.TipoProcessoCodigo;
        Categoria = payload.Categoria;
        RegraCodigo = payload.RegraCodigo;
        Predicado = predicado;
        DescricaoHumana = payload.DescricaoHumana;
        BaseLegal = payload.BaseLegal;
        AtoNormativoUrl = payload.AtoNormativoUrl;
        PortariaInternaCodigo = payload.PortariaInternaCodigo;
        VigenciaInicio = payload.VigenciaInicio;
        VigenciaFim = payload.VigenciaFim;
        Hash = ComputeHash();
    }

    /// <summary>
    /// Factory canônica (Story #460) — devolve uma regra na forma plena com
    /// hash já computado e payload normalizado pelo
    /// <see cref="ObrigatoriedadeLegalPayloadNormalizer"/>.
    /// </summary>
    public static Result<ObrigatoriedadeLegal> Criar(
        string tipoProcessoCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string descricaoHumana,
        string baseLegal,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim = null,
        string? atoNormativoUrl = null,
        string? portariaInternaCodigo = null)
    {
        if (predicado is null)
        {
            return Result<ObrigatoriedadeLegal>.Failure(new DomainError(
                "ObrigatoriedadeLegal.PredicadoObrigatorio",
                "Predicado é obrigatório."));
        }

        Result forma = ValidarFormaDoPredicado(predicado);
        if (forma.IsFailure)
        {
            return Result<ObrigatoriedadeLegal>.Failure(forma.Error!);
        }

        Result<NormalizedPayload> normalized = ObrigatoriedadeLegalPayloadNormalizer.Normalizar(
            tipoProcessoCodigo,
            categoria,
            regraCodigo,
            descricaoHumana,
            baseLegal,
            atoNormativoUrl,
            portariaInternaCodigo,
            vigenciaInicio,
            vigenciaFim);

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
    /// Fonte de "hoje" para <c>VigenciaInicio</c>. Obrigatório (sem default
    /// <see cref="TimeProvider.System"/>): a convenção de relógio exige que o
    /// <see cref="TimeProvider"/> seja sempre injetado. Testes passam um
    /// <see cref="TimeProvider"/> fake para isolar o cenário do relógio.
    /// </param>
    public static Result<ObrigatoriedadeLegal> Criar(
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string baseLegal,
        string descricaoHumana,
        string? portariaInternaCodigo,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return Criar(
            tipoProcessoCodigo: TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: predicado,
            descricaoHumana: descricaoHumana,
            baseLegal: baseLegal,
            vigenciaInicio: DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.Date),
            vigenciaFim: null,
            atoNormativoUrl: null,
            portariaInternaCodigo: portariaInternaCodigo);
    }

    /// <summary>
    /// Atualiza os campos semânticos da regra (Story #461 admin PUT) e
    /// recomputa o hash. O caller é responsável por respeitar a política
    /// de versionamento documentada no ADR-0058 (soft-delete + new row vs
    /// in-place update); esta entidade só garante consistência interna.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Semântica full-replace</strong>: todos os campos (inclusive
    /// opcionais) são aplicados literalmente ao estado da regra. O caller
    /// que só quer alterar um campo precisa repassar os valores atuais
    /// dos demais — sem parâmetros opcionais com default <c>null</c>, para
    /// evitar limpeza silenciosa de <c>AtoNormativoUrl</c> ou
    /// <c>PortariaInternaCodigo</c> persistidos previamente.
    /// </para>
    /// </remarks>
    public Result Atualizar(
        string tipoProcessoCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string descricaoHumana,
        string baseLegal,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        string? atoNormativoUrl,
        string? portariaInternaCodigo)
    {
        if (predicado is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.PredicadoObrigatorio",
                "Predicado é obrigatório."));
        }

        Result forma = ValidarFormaDoPredicado(predicado);
        if (forma.IsFailure)
        {
            return forma;
        }

        Result<NormalizedPayload> normalized = ObrigatoriedadeLegalPayloadNormalizer.Normalizar(
            tipoProcessoCodigo,
            categoria,
            regraCodigo,
            descricaoHumana,
            baseLegal,
            atoNormativoUrl,
            portariaInternaCodigo,
            vigenciaInicio,
            vigenciaFim);

        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error!);
        }

        AplicarPayload(normalized.Value!, predicado);
        return Result.Success();
    }

    /// <summary>
    /// Recusa predicado cujo conteúdo o tornaria impossível de avaliar ou
    /// vacuamente verdadeiro. Valida <b>forma</b>, não existência: se o código
    /// referenciado corresponde a um cadastro vivo é pergunta com I/O, que o
    /// handler responde (ADR-0125).
    /// </summary>
    /// <remarks>
    /// <para>Público porque a ordem importa: o handler confere a forma <b>antes</b> de
    /// resolver as referências no cadastro. Um código em branco vira busca por string
    /// vazia, que o cadastro legitimamente não encontra — e a recusa sairia como
    /// "código não existe", escondendo do cliente que o campo simplesmente não foi
    /// preenchido. A factory chama esta mesma validação, então a regra continua com
    /// uma fonte só.</para>
    /// <para>Uma exigência de modalidades mínimas sem nenhuma modalidade é aprovada por
    /// vacuidade em <c>AvaliadorConformidadeLegal</c> — a regra existe, aparece
    /// como cumprida e não exige nada de ninguém. Código em branco tem o mesmo
    /// efeito: nunca casa com o valor congelado no processo, e a cláusula vira
    /// letra morta sem que nada sinalize.</para>
    /// </remarks>
    public static Result ValidarFormaDoPredicado(PredicadoObrigatoriedade predicado)
    {
        switch (predicado)
        {
            case ModalidadesMinimas modalidades
                when modalidades.Codigos is null || modalidades.Codigos.Count == 0:
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.ModalidadesMinimasVazia",
                    "Exigência de modalidades mínimas precisa de ao menos um código de modalidade."));

            case ModalidadesMinimas modalidades
                when modalidades.Codigos.Any(string.IsNullOrWhiteSpace):
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.ModalidadesMinimasVazia",
                    "Exigência de modalidades mínimas não admite código em branco na lista."));

            case EtapaObrigatoria etapa when string.IsNullOrWhiteSpace(etapa.TipoEtapaCodigo):
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco",
                    "Exigência de etapa obrigatória precisa do código do tipo de etapa."));

            case DocumentoObrigatorioParaModalidade documento
                when string.IsNullOrWhiteSpace(documento.Modalidade)
                    || string.IsNullOrWhiteSpace(documento.TipoDocumento):
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco",
                    "Exigência de documento por modalidade precisa do código da modalidade e do tipo de documento."));

            case AtendimentoDisponivel atendimento
                when atendimento.Necessidades is null || atendimento.Necessidades.Count == 0:
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.AtendimentoDisponivelVazio",
                    "Exigência de atendimento disponível precisa de ao menos um código de tipo de deficiência."));

            case AtendimentoDisponivel atendimento
                when atendimento.Necessidades.Any(string.IsNullOrWhiteSpace):
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco",
                    "Exigência de atendimento disponível não admite código em branco na lista."));

            case DesempateDeveIncluir desempate when string.IsNullOrWhiteSpace(desempate.Criterio):
                return Result.Failure(new DomainError(
                    "ObrigatoriedadeLegal.PredicadoComCodigoEmBranco",
                    "Exigência de critério de desempate precisa do código do critério."));

            default:
                return Result.Success();
        }
    }

    private void AplicarPayload(NormalizedPayload payload, PredicadoObrigatoriedade predicado)
    {
        TipoProcessoCodigo = payload.TipoProcessoCodigo;
        Categoria = payload.Categoria;
        RegraCodigo = payload.RegraCodigo;
        Predicado = predicado;
        DescricaoHumana = payload.DescricaoHumana;
        BaseLegal = payload.BaseLegal;
        AtoNormativoUrl = payload.AtoNormativoUrl;
        PortariaInternaCodigo = payload.PortariaInternaCodigo;
        VigenciaInicio = payload.VigenciaInicio;
        VigenciaFim = payload.VigenciaFim;
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
        TipoProcessoCodigo,
        Categoria,
        RegraCodigo,
        Predicado,
        BaseLegal,
        PortariaInternaCodigo,
        VigenciaInicio,
        VigenciaFim);
}
