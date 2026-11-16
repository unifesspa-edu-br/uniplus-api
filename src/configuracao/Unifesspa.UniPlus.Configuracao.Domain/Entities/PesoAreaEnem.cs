namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Pesos por Área do ENEM, por grupo de área — materializa o Anexo I da Resolução
/// nº 805/2024/Consepe: para cada grupo de área (<see cref="GrupoCurso"/>), o peso de
/// cada área de conhecimento e, opcionalmente, a nota mínima (corte) de cada área.
/// </summary>
/// <remarks>
/// <para><b>Área só existe aqui.</b> As cinco áreas (código, rótulo oficial e ordem) são
/// definidas por <see cref="Areas"/> e gravadas pelo sistema em cada linha; o operador
/// edita só os valores. Nenhuma outra parte do sistema enumera áreas: o processo seletivo
/// congela por cópia o Peso por Área que usa, com as áreas dentro.</para>
/// <para>Versionável por <c>Resolucao</c>: cada resolução gera uma linha por grupo. A
/// chave de negócio é o par (<c>Resolucao</c>, <c>GrupoCurso</c>), único entre linhas
/// vivas — validado pelo handler e reforçado por índice único parcial de banco
/// (<c>WHERE is_deleted = false</c>). O par e o <c>Id</c> são imutáveis na atualização —
/// mudar resolução ou grupo caracterizaria outra linha, não uma edição.</para>
/// <para>Dado institucional de referência, sem PII (LGPD inaplicável). Nenhuma FK aponta
/// para este cadastro: a ligação <c>curso.grupo_area_enem</c> é por valor sobre o
/// vocabulário de grupos, e o congelamento no processo seletivo (ADR-0061) é cópia por
/// valor — por isso a remoção lógica nunca é bloqueada por referência.</para>
/// </remarks>
public sealed class PesoAreaEnem : SoftDeletableEntity, IAuditableEntity
{
    public const string CodigoRedacao = AreaDoEnem.Redacao;
    public const string CodigoCienciasDaNatureza = AreaDoEnem.CienciasDaNatureza;
    public const string CodigoCienciasHumanas = AreaDoEnem.CienciasHumanas;
    public const string CodigoLinguagens = AreaDoEnem.Linguagens;
    public const string CodigoMatematica = AreaDoEnem.Matematica;

    /// <summary>
    /// Teto de cada peso de área — limite da precisão persistida (<c>numeric(4,2)</c>).
    /// Um valor acima disso estouraria a coluna; o guard transforma o overflow num
    /// erro de domínio (422) em vez de 500.
    /// </summary>
    public const decimal PesoMaximo = 99.99m;

    /// <summary>Nota máxima de uma área do ENEM (escala 0–1000) — teto do corte.</summary>
    public const decimal CorteMaximo = 1000m;

    private const int ResolucaoMinLength = 1;
    private const int ResolucaoMaxLength = 40;

    /// <summary>
    /// As cinco áreas do cadastro, na ordem canônica de exibição e de leitura, com o
    /// rótulo do Anexo I da Resolução nº 805/2024/Consepe, a resolução que fundamenta os
    /// pesos — por isso "Linguagens e suas Tecnologias", e não a grafia do INEP.
    /// </summary>
    public static IReadOnlyList<AreaEnemDescrita> Areas { get; } =
    [
        new(CodigoRedacao, "Redação"),
        new(CodigoCienciasDaNatureza, "Ciências da Natureza e suas Tecnologias"),
        new(CodigoCienciasHumanas, "Ciências Humanas e suas Tecnologias"),
        new(CodigoLinguagens, "Linguagens e suas Tecnologias"),
        new(CodigoMatematica, "Matemática e suas Tecnologias"),
    ];

    internal static readonly Dictionary<string, int> OrdemPorCodigo =
        Areas.Select(static (area, indice) => (area.Codigo, indice))
            .ToDictionary(static par => par.Codigo, static par => par.indice, StringComparer.Ordinal);

    private readonly List<PesoAreaEnemArea> _areas = [];

    public string Resolucao { get; private set; } = string.Empty;
    public GrupoCurso GrupoCurso { get; private set; } = null!;

    // Rótulo do grupo, gravado ao lado do código. O domínio lê o rótulo do próprio
    // GrupoCurso; a coluna existe para o dado persistido carregar código e rótulo.
    private string _grupoCursoRotulo = string.Empty;

    /// <summary>O peso e o corte de cada uma das cinco áreas, sempre na ordem canônica.</summary>
    /// <remarks>
    /// Um código que não esteja em <see cref="Areas"/> (linha gravada fora do agregado)
    /// vai para o fim em vez de derrubar a leitura da linha inteira.
    /// </remarks>
    public IReadOnlyList<PesoAreaEnemArea> AreasDaLinha =>
        [.. _areas.OrderBy(static area => OrdemPorCodigo.GetValueOrDefault(area.Codigo, int.MaxValue))];

    public string BaseLegal { get; private set; } = string.Empty;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private PesoAreaEnem()
    {
    }

    /// <summary>
    /// Cria uma nova linha de pesos, acumulando toda violação independente em vez de
    /// parar na primeira: resolução, grupo de área (domínio fechado), as cinco áreas
    /// (cada uma uma vez, peso na faixa, corte opcional na faixa) e a base legal. A
    /// unicidade do par (<paramref name="resolucao"/>, <paramref name="grupoCurso"/>)
    /// entre linhas vivas é responsabilidade do handler.
    /// </summary>
    public static Result<PesoAreaEnem> Criar(
        string? resolucao,
        string? grupoCurso,
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        (string? resolucaoNorm, DomainError? erroDaResolucao) = ValidarResolucao(resolucao);
        if (erroDaResolucao is not null)
        {
            erros.Add(new("resolucao", erroDaResolucao));
        }

        Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoCurso);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoCurso", new DomainError(
                PesoAreaEnemErrorCodes.GrupoCursoInvalido, grupo.Error!.Message)));
        }

        Result<CamposEditaveis> valores = ValidarAreasEBaseLegal(areas, baseLegal);
        if (valores.IsFailure)
        {
            erros.AddRange(valores.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<PesoAreaEnem>.ValidationFailure(erros);
        }

        var peso = new PesoAreaEnem
        {
            Resolucao = resolucaoNorm!,
            GrupoCurso = grupo.Value!,
            _grupoCursoRotulo = grupo.Value!.Rotulo,
            BaseLegal = valores.Value!.BaseLegal,
        };
        peso._areas.AddRange(valores.Value.Areas.Select(static area =>
            PesoAreaEnemArea.Criar(area.Descrita.Codigo, area.Descrita.Rotulo, area.Peso, area.Corte)));

        return Result<PesoAreaEnem>.Success(peso);
    }

    /// <summary>
    /// Atualiza o peso e o corte de cada área e a base legal, acumulando toda violação
    /// independente. Código e rótulo das áreas não mudam — são fixos —, e nunca altera o
    /// <c>Id</c>, a <c>Resolucao</c> nem o <c>GrupoCurso</c> (chave de negócio imutável).
    /// </summary>
    public Result Atualizar(
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal)
    {
        Result<CamposEditaveis> valores = ValidarAreasEBaseLegal(areas, baseLegal);
        if (valores.IsFailure)
        {
            return Result.ValidationFailure(valores.Errors);
        }

        // Os códigos são fixos: a atualização muda os valores de cada linha filha no
        // lugar, em vez de trocar a coleção, e regrava o rótulo a partir de Areas — um
        // rótulo corrigido no domínio chega às linhas já gravadas na próxima edição. Toda
        // linha nasce com as cinco áreas pelo Criar, e a migration excluiu as antigas: só
        // SQL escrito fora do agregado deixa uma linha incompleta, e aí a edição falha.
        foreach (AreaValidada area in valores.Value!.Areas)
        {
            PesoAreaEnemArea existente = _areas.Find(a => a.Codigo == area.Descrita.Codigo)
                ?? throw new InvalidOperationException(
                    $"A linha de pesos {Id} não tem a área {area.Descrita.Codigo}; toda linha é criada com as cinco.");
            existente.AplicarValores(area.Descrita.Rotulo, area.Peso, area.Corte);
        }

        // O grupo não muda (é chave de negócio), mas o rótulo gravado ao lado do código é
        // regravado a partir do domínio pelo mesmo motivo do rótulo das áreas: uma correção
        // no rótulo do grupo chega às linhas já gravadas na próxima edição.
        _grupoCursoRotulo = GrupoCurso.Rotulo;
        BaseLegal = valores.Value.BaseLegal;
        return Result.Success();
    }

    /// <summary>
    /// Valida as áreas e a base legal — os únicos campos editáveis na atualização — sem
    /// I/O e sem mutar nada. Para o handler de atualização falhar rápido antes de buscar
    /// a linha por Id (validação sempre vence 404).
    /// </summary>
    public static Result ValidarCamposDoPayload(
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal)
    {
        Result<CamposEditaveis> resultado = ValidarAreasEBaseLegal(areas, baseLegal);
        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : Result.Success();
    }

    private static Result<CamposEditaveis> ValidarAreasEBaseLegal(
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal)
    {
        List<FieldError> erros = [];
        IReadOnlyList<AreaValidada> validas = ConferenciaDasAreasDoPeso.Conferir(erros, areas);
        string? baseLegalNormalizada = ConferenciaDaBaseLegalDoPeso.Conferir(erros, baseLegal);

        return erros.Count == 0
            ? Result<CamposEditaveis>.Success(new CamposEditaveis(validas, baseLegalNormalizada!))
            : Result<CamposEditaveis>.ValidationFailure(erros);
    }

    /// <summary>
    /// A resolução na forma em que o cadastro a grava — aparada e em NFC —, ou
    /// <see langword="null"/> quando o texto não pode ser uma resolução gravada: em branco,
    /// com caractere invisível ou fora do tamanho da coluna. É por esse valor que o processo
    /// seletivo congela a resolução e que a busca por resolução compara.
    /// </summary>
    public static string? NormalizarResolucao(string? resolucao) => ValidarResolucao(resolucao).Normalizada;

    // A resolução volta nas mensagens de recusa e nos logs: além de caber na coluna, não pode
    // trazer quebra de linha nem inversão de direção de leitura.
    private static (string? Normalizada, DomainError? Erro) ValidarResolucao(string? resolucao)
    {
        if (string.IsNullOrWhiteSpace(resolucao))
        {
            return (null, new DomainError(PesoAreaEnemErrorCodes.ResolucaoObrigatoria, "Resolução é obrigatória."));
        }

        return TextoNormalizavel.TentarNfc(resolucao.Trim(), ResolucaoMaxLength, out string normalizada) switch
        {
            SituacaoDoTexto.Valido => (normalizada, null),
            SituacaoDoTexto.CaractereInvalido => (null, new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoCaractereInvalido,
                "Resolução não pode conter caractere de controle, de formatação, de quebra de linha ou que não seja texto.")),
            _ => (null, new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoTamanho,
                $"Resolução deve ter entre {ResolucaoMinLength} e {ResolucaoMaxLength} caracteres.")),
        };
    }
}
