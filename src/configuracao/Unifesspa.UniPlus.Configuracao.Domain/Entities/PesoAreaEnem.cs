namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using System.Globalization;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

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
    public const string CodigoRedacao = "REDACAO";
    public const string CodigoCienciasDaNatureza = "CIENCIAS_DA_NATUREZA";
    public const string CodigoCienciasHumanas = "CIENCIAS_HUMANAS";
    public const string CodigoLinguagens = "LINGUAGENS";
    public const string CodigoMatematica = "MATEMATICA";

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
    private const int BaseLegalMaxLength = 500;

    /// <summary>Escala persistida dos pesos (<c>numeric(4,2)</c>).</summary>
    private const int EscalaPeso = 2;

    /// <summary>Escala persistida do corte (<c>numeric(7,3)</c>).</summary>
    private const int EscalaCorte = 3;

    private const int TamanhoMaximoDoCodigoEcoado = 40;

    /// <summary>
    /// Teto de itens aceitos em <c>areas</c>: acima dele a lista é recusada inteira, sem
    /// um erro por item — uma entrada pequena não pode gerar resposta desproporcional.
    /// </summary>
    private const int MaximoDeItensInformados = 20;

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

    private static readonly Dictionary<string, int> OrdemPorCodigo =
        Areas.Select(static (area, indice) => (area.Codigo, indice))
            .ToDictionary(static par => par.Codigo, static par => par.indice, StringComparer.Ordinal);

    // Declarado depois de Areas: inicializadores estáticos rodam na ordem do texto.
    private static readonly string AreasAceitas =
        string.Join(", ", Areas.Select(static a => $"{a.Codigo} ({a.Rotulo})"));

    private readonly List<PesoAreaEnemArea> _areas = [];

    public string Resolucao { get; private set; } = string.Empty;
    public GrupoCurso GrupoCurso { get; private set; } = null!;

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

        string? resolucaoNorm = null;
        if (string.IsNullOrWhiteSpace(resolucao))
        {
            erros.Add(new("resolucao", new DomainError(
                PesoAreaEnemErrorCodes.ResolucaoObrigatoria, "Resolução é obrigatória.")));
        }
        else
        {
            resolucaoNorm = resolucao.Trim();
            if (resolucaoNorm.Length is < ResolucaoMinLength or > ResolucaoMaxLength)
            {
                erros.Add(new("resolucao", new DomainError(
                    PesoAreaEnemErrorCodes.ResolucaoTamanho,
                    $"Resolução deve ter entre {ResolucaoMinLength} e {ResolucaoMaxLength} caracteres.")));
                resolucaoNorm = null;
            }
        }

        Result<GrupoCurso> grupo = GrupoCurso.Criar(grupoCurso);
        if (grupo.IsFailure)
        {
            erros.Add(new("grupoCurso", new DomainError(
                PesoAreaEnemErrorCodes.GrupoCursoInvalido, grupo.Error!.Message)));
        }

        Result<IReadOnlyList<AreaValidada>> valores = ValidarAreasEBaseLegal(areas, baseLegal);
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
            BaseLegal = (baseLegal ?? string.Empty).Trim(),
        };
        peso._areas.AddRange(valores.Value!.Select(static area =>
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
        Result<IReadOnlyList<AreaValidada>> valores = ValidarAreasEBaseLegal(areas, baseLegal);
        if (valores.IsFailure)
        {
            return Result.ValidationFailure(valores.Errors);
        }

        // Os códigos são fixos: a atualização muda os valores de cada linha filha no
        // lugar, em vez de trocar a coleção, e regrava o rótulo a partir de Areas — um
        // rótulo corrigido no domínio chega às linhas já gravadas na próxima edição. Toda
        // linha nasce com as cinco áreas pelo Criar, e a migration excluiu as antigas: só
        // SQL escrito fora do agregado deixa uma linha incompleta, e aí a edição falha.
        foreach (AreaValidada area in valores.Value!)
        {
            PesoAreaEnemArea existente = _areas.Find(a => a.Codigo == area.Descrita.Codigo)
                ?? throw new InvalidOperationException(
                    $"A linha de pesos {Id} não tem a área {area.Descrita.Codigo}; toda linha é criada com as cinco.");
            existente.AplicarValores(area.Descrita.Rotulo, area.Peso, area.Corte);
        }

        BaseLegal = (baseLegal ?? string.Empty).Trim();
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
        Result<IReadOnlyList<AreaValidada>> resultado = ValidarAreasEBaseLegal(areas, baseLegal);
        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : Result.Success();
    }

    // Cada área informada precisa ser uma das cinco, uma vez só; nenhuma pode faltar. O
    // erro vai no campo da área (`areas[i].codigo`, `.peso`, `.corte`), e a falta, em
    // `areas`, nomeando os códigos ausentes. Toda violação independente se acumula.
    private static Result<IReadOnlyList<AreaValidada>> ValidarAreasEBaseLegal(
        IReadOnlyList<AreaInformada>? areas,
        string? baseLegal)
    {
        List<FieldError> erros = [];
        List<AreaValidada> validas = [];
        HashSet<string> vistas = new(StringComparer.Ordinal);

        if (areas is { Count: > MaximoDeItensInformados })
        {
            erros.Add(new("areas", new DomainError(
                PesoAreaEnemErrorCodes.AreasEmExcesso,
                $"Informe as cinco áreas, uma vez cada; vieram {areas.Count} itens.")));
            AdicionarErrosDeBaseLegal(erros, baseLegal);
            return Result<IReadOnlyList<AreaValidada>>.ValidationFailure(erros);
        }

        for (int i = 0; i < (areas?.Count ?? 0); i++)
        {
            AreaInformada informada = areas![i];
            string? codigo = informada.Codigo?.Trim();
            decimal peso = informada.Peso;
            decimal? corte = informada.Corte;
            string campo = $"areas[{i}]";

            AreaEnemDescrita? descrita = codigo is not null && OrdemPorCodigo.TryGetValue(codigo, out int ordem)
                ? Areas[ordem]
                : null;
            if (descrita is null)
            {
                string mensagem = string.IsNullOrEmpty(codigo)
                    ? $"Informe o código da área. Aceitas: {AreasAceitas}."
                    : $"\"{Resumir(codigo)}\" não é uma área do Peso por Área. Aceitas: {AreasAceitas}.";
                erros.Add(new($"{campo}.codigo", new DomainError(
                    PesoAreaEnemErrorCodes.AreaForaDoDominio, mensagem)));
            }

            bool repetida = false;
            if (descrita is not null && !vistas.Add(descrita.Codigo))
            {
                erros.Add(new($"{campo}.codigo", new DomainError(
                    PesoAreaEnemErrorCodes.AreaRepetida,
                    $"A área {descrita.Codigo} ({descrita.Rotulo}) foi informada mais de uma vez.")));
                repetida = true;
            }

            // A área repetida continua conhecida: peso e corte dela são conferidos pela
            // regra da própria área, só não entram na linha.
            string nomeDaArea = descrita?.Rotulo ?? "esta área";
            int errosAntes = erros.Count;
            AdicionarErroDePeso(erros, campo, peso, nomeDaArea);
            AdicionarErroDeCorte(erros, campo, corte, descrita);

            if (descrita is not null && !repetida && erros.Count == errosAntes)
            {
                validas.Add(new AreaValidada(
                    descrita,
                    Arredondar(peso, EscalaPeso),
                    corte is null ? null : Arredondar(corte.Value, EscalaCorte)));
            }
        }

        string[] faltando = [.. Areas.Select(static a => a.Codigo).Where(c => !vistas.Contains(c))];
        if (faltando.Length > 0)
        {
            erros.Add(new("areas", new DomainError(
                PesoAreaEnemErrorCodes.AreaFaltando,
                $"Informe o peso das cinco áreas; faltam: {string.Join(", ", faltando)}.")));
        }

        AdicionarErrosDeBaseLegal(erros, baseLegal);

        return erros.Count == 0
            ? Result<IReadOnlyList<AreaValidada>>.Success(validas)
            : Result<IReadOnlyList<AreaValidada>>.ValidationFailure(erros);
    }

    private static void AdicionarErrosDeBaseLegal(List<FieldError> erros, string? baseLegal)
    {
        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            erros.Add(new("baseLegal", new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalObrigatoria, "Base legal é obrigatória.")));
        }
        else if (baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                PesoAreaEnemErrorCodes.BaseLegalTamanho,
                $"Base legal deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }
    }

    private static void AdicionarErroDePeso(List<FieldError> erros, string campo, decimal peso, string nomeDaArea)
    {
        if (peso < 0)
        {
            erros.Add(new($"{campo}.peso", new DomainError(
                PesoAreaEnemErrorCodes.PesoNegativo,
                $"O peso informado para {nomeDaArea} não pode ser negativo.")));
        }
        else if (peso > PesoMaximo)
        {
            erros.Add(new($"{campo}.peso", new DomainError(
                PesoAreaEnemErrorCodes.PesoExcedeMaximo,
                $"O peso informado para {nomeDaArea} não pode exceder {PesoMaximo}.")));
        }
    }

    private static void AdicionarErroDeCorte(List<FieldError> erros, string campo, decimal? corte, AreaEnemDescrita? area)
    {
        if (corte is null)
        {
            return;
        }

        // Enquanto a eliminação por corte só conhece a Redação, um corte em outra área
        // seria valor configurado sem efeito nenhum: a recusa sai quando a regra de
        // eliminação passar a aceitar a área. É a única recusa do campo nesse caso —
        // a faixa não importa quando o caminho é deixar a área sem corte.
        if (area is not null && area.Codigo != CodigoRedacao)
        {
            erros.Add(new($"{campo}.corte", new DomainError(
                PesoAreaEnemErrorCodes.CorteForaDaRedacao,
                $"Por enquanto só a Redação aceita corte; {area.Rotulo} deve ficar sem corte.")));
            return;
        }

        string nomeDaArea = area?.Rotulo ?? "esta área";
        if (corte < 0)
        {
            erros.Add(new($"{campo}.corte", new DomainError(
                PesoAreaEnemErrorCodes.CorteNegativo,
                $"O corte informado para {nomeDaArea} não pode ser negativo.")));
        }
        else if (corte > CorteMaximo)
        {
            erros.Add(new($"{campo}.corte", new DomainError(
                PesoAreaEnemErrorCodes.CorteExcedeMaximo,
                $"O corte informado para {nomeDaArea} não pode exceder {CorteMaximo} (nota máxima de uma área do ENEM).")));
        }

    }

    // O código recusado volta na mensagem para o operador achar o erro, mas limitado:
    // um valor arbitrariamente longo não é ecoado inteiro na resposta nem nos logs.
    // O corte nunca parte um par substituto: um emoji na fronteira sai inteiro ou não sai.
    // Caracteres de controle, de formatação e separadores de linha e parágrafo (quebra de
    // linha, controles bidi, U+2028/U+2029) viram "?":
    // o texto volta na resposta e nos logs, e não pode reescrever a linha em que cai.
    private static string Resumir(string texto)
    {
        string parte = texto;
        string reticencias = string.Empty;
        if (texto.Length > TamanhoMaximoDoCodigoEcoado)
        {
            int corte = char.IsHighSurrogate(texto[TamanhoMaximoDoCodigoEcoado - 1])
                ? TamanhoMaximoDoCodigoEcoado - 1
                : TamanhoMaximoDoCodigoEcoado;
            parte = texto[..corte];
            reticencias = "…";
        }

        return string.Concat(parte.Select(static c =>
            char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? '?' : c)) + reticencias;
    }

    private static decimal Arredondar(decimal valor, int escala) =>
        Math.Round(valor, escala, MidpointRounding.ToEven);

    private sealed record AreaValidada(AreaEnemDescrita Descrita, decimal Peso, decimal? Corte);
}
