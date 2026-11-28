namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Tipo de fase (UNI-REQ-0139): nomeia, em domínio fechado, os momentos do ciclo
/// de vida de um processo seletivo (inscrição, homologação, avaliação, recursos,
/// resultado, matrícula…). Dado institucional de referência sem PII (LGPD
/// inaplicável). Não é Etapa — a Etapa pontuada pertence a outro requisito; aqui o
/// sinalizador <see cref="SinalizadoresDaFase.AgrupaEtapas"/> apenas marca qual fase as agrupa.
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoFase"/>) é a chave
/// natural, único entre fases vivas (índice único parcial <c>WHERE is_deleted =
/// false</c>) e <b>imutável</b>: o comando de atualização não o aceita — o
/// vocabulário canônico é fixo. Além do formato, o código deve pertencer ao
/// conjunto canônico de fases (<see cref="FaseCanonicaCatalogo"/>).</para>
/// <para>As invariantes de coerência dos <see cref="Sinalizadores"/> com o código (agrupamento
/// de etapas só para a fase de avaliação, complementação só nas fases legalmente permitidas,
/// marca de solicitação de isenção só na fase de isenção) e entre eles (coletar inscrição e
/// solicitação de isenção são exclusivos) são conferidas pela factory <see cref="Criar"/> e por
/// <see cref="Atualizar"/>, revalidadas contra o código imutável. A remoção é sempre soft-delete; nunca bloqueada — o único
/// consumo é por cópia dos dados no Módulo Seleção (ADR-0061), e não há
/// FK intra-banco apontando para esta entidade.</para>
/// <para>O que uma fase publica e o papel de cada publicação são declarados na fase
/// do cronograma do processo, não aqui: quem monta o edital é quem sabe se aquela
/// homologação sai preliminar, definitiva ou as duas. <see cref="SinalizadoresDaFase.ColetaInscricao"/>
/// decide o piso mínimo do cronograma quando a origem dos candidatos é inscrição
/// própria; <see cref="OrigemData"/>
/// decide se o período da fase é obrigatório (<c>NO_EDITAL</c>) ou opcional
/// (<c>NA_EXECUCAO</c>). O cadastro <b>nasce semeado</b> com as fases do vocabulário
/// (ADR-0113, Emenda 1): elas existem porque o ciclo do certame as tem, e não há
/// autor a registrar — daí <c>CreatedBy</c> nulo nas linhas do seed. O CRUD admin
/// continua sendo o caminho para editar o que se administra de fato: nome,
/// descrição e base legal.</para>
/// </remarks>
public sealed class FaseCanonica : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 300;
    private const int BaseLegalMaxLength = 500;

    public CodigoFase Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public string? BaseLegal { get; private set; }

    /// <summary>
    /// O que a fase faz no certame: agrupa etapas, admite complementação, coleta inscrição ou coleta
    /// solicitação de isenção. Substituído por inteiro a cada atualização.
    /// </summary>
    /// <remarks>
    /// Marcas booleanas, e não comparação com o código: a Seleção decide pelos atributos que copia
    /// para a cópia da fase, nunca pelo código (ADR-0113). O código é imutável e as marcas o
    /// acompanham.
    /// </remarks>
    public SinalizadoresDaFase Sinalizadores { get; private set; } = null!;

    /// <summary>Quando a data da fase é fixada: <see cref="OrigemDataFase.NoEdital"/> (período obrigatório) ou <see cref="OrigemDataFase.NaExecucao"/> (período opcional).</summary>
    public OrigemDataFase OrigemData { get; private set; }

    /// <summary>Id da unidade que costuma responder pela fase, copiado sem FK (ADR-0061); nulo quando não há sugestão.</summary>
    public Guid? UnidadeSugeridaOrigemId { get; private set; }

    /// <summary>Sigla da unidade sugerida, copiada ao cadastrar ou editar a fase.</summary>
    public string? UnidadeSugeridaSigla { get; private set; }

    /// <summary>Nome da unidade sugerida, copiado ao cadastrar ou editar a fase.</summary>
    public string? UnidadeSugeridaNome { get; private set; }

    /// <summary>A unidade sugerida como valor, ou <see langword="null"/> quando a fase não a tem.</summary>
    public UnidadeSugerida? UnidadeSugerida =>
        UnidadeSugeridaOrigemId is { } origemId
            ? new UnidadeSugerida(origemId, UnidadeSugeridaSigla!, UnidadeSugeridaNome!)
            : null;

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private FaseCanonica()
    {
    }

    /// <summary>
    /// Valida o código: formato fechado (<see cref="CodigoFase.Criar"/>) e pertença
    /// ao conjunto canônico de fases. Sem mutar nada — existe para
    /// <see cref="Criar"/> decidir se as coerências dos <see cref="SinalizadoresDaFase"/> que
    /// dependem do código fazem sentido de avaliar, e para o handler consultar unicidade só com
    /// um código já confiável.
    /// </summary>
    public static Result<CodigoFase> ValidarCodigo(string? codigo)
    {
        Result<CodigoFase> codigoResult = CodigoFase.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<CodigoFase>.ValidationFailure([new("codigo", codigoResult.Error!)]);
        }

        if (!FaseCanonicaCatalogo.EhCanonico(codigoResult.Value!.Valor))
        {
            return Result<CodigoFase>.ValidationFailure([new("codigo", new DomainError(
                FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico,
                "Código não pertence ao conjunto canônico de fases."))]);
        }

        return Result<CodigoFase>.Success(codigoResult.Value!);
    }

    /// <summary>
    /// Recusa o que o corpo da atualização decide sem o código da fase: os quatro campos comuns
    /// (nome, descrição, base legal e origem da data), a ausência de cada sinalizador e a
    /// exclusividade entre coletar inscrição e coletar solicitação de isenção, acumulados nesta ordem. Sem mutar nada e sem depender de
    /// I/O — existe para o handler de atualização falhar rápido antes de buscar a fase por Id.
    /// O que depende do código gravado fica para <see cref="Atualizar"/>.
    /// </summary>
    public static Result ValidarOQueNaoDependeDoCodigo(
        string? nome, string? descricao, string? baseLegal, string? origemData, SinalizadoresInformados sinalizadores)
    {
        ArgumentNullException.ThrowIfNull(sinalizadores);
        List<FieldError> erros =
        [
            .. ValidarCamposComuns(nome, descricao, baseLegal, origemData).Errors,
            .. sinalizadores.Recusas(codigoReconhecido: null),
        ];

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Valida e normaliza os quatro campos editáveis que <b>não</b> dependem do
    /// código da fase (nome, descrição, base legal e origem da data). Roda as quatro regras
    /// sempre, nesta ordem, gravando toda violação independente na mesma lista em vez de parar
    /// na primeira. Sem mutar nada e sem depender de I/O.
    /// </summary>
    private static Result<(string Nome, string? Descricao, string? BaseLegal, OrigemDataFase OrigemData)> ValidarCamposComuns(
        string? nome, string? descricao, string? baseLegal, string? origemData)
    {
        List<FieldError> erros = [];
        string? nomeNormalizado = ValidarNome(nome, erros);
        string? descricaoNormalizada = NormalizarDentroDoLimite(
            descricao, "descricao", DescricaoMaxLength, FaseCanonicaErrorCodes.DescricaoTamanho, "Descrição da fase", erros);
        string? baseLegalNormalizada = NormalizarDentroDoLimite(
            baseLegal, "baseLegal", BaseLegalMaxLength, FaseCanonicaErrorCodes.BaseLegalTamanho, "Base legal da fase", erros);
        OrigemDataFase origem = ValidarOrigemData(origemData, erros);

        return erros.Count > 0
            ? Result<(string, string?, string?, OrigemDataFase)>.ValidationFailure(erros)
            : Result<(string, string?, string?, OrigemDataFase)>.Success(
                (nomeNormalizado!, descricaoNormalizada, baseLegalNormalizada, origem));
    }

    /// <summary>
    /// Cria um novo tipo de fase. Valida o código (formato + pertença ao conjunto
    /// canônico), os quatro campos comuns e os <paramref name="sinalizadores"/> — a coerência
    /// deles com o código só é conferida quando o código é reconhecido —, acumulando toda
    /// violação na mesma resposta. A unicidade do código entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<FaseCanonica> Criar(
        string? codigo,
        string? nome,
        string? descricao,
        string? baseLegal,
        string? origemData,
        SinalizadoresInformados sinalizadores)
    {
        ArgumentNullException.ThrowIfNull(sinalizadores);
        Result<CodigoFase> codigoResult = ValidarCodigo(codigo);
        Result<(string Nome, string? Descricao, string? BaseLegal, OrigemDataFase OrigemData)> comuns =
            ValidarCamposComuns(nome, descricao, baseLegal, origemData);
        List<FieldError> erros = [.. codigoResult.Errors, .. comuns.Errors, .. sinalizadores.Recusas(codigoResult.Value)];
        if (erros.Count > 0)
        {
            return Result<FaseCanonica>.ValidationFailure(erros);
        }

        var fase = new FaseCanonica { Codigo = codigoResult.Value! };
        fase.AplicarCampos(comuns.Value, sinalizadores);

        return Result<FaseCanonica>.Success(fase);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da fase. O <c>Codigo</c> e o <c>Id</c> são
    /// <b>imutáveis</b> — este método não os recebe. Revalida os quatro campos comuns
    /// e os <paramref name="sinalizadores"/> contra o código gravado da fase, acumulando toda
    /// violação na mesma resposta. Assim, também confere a coerência com o código gravado
    /// da fase, que não está disponível antes desta chamada.
    /// </summary>
    public Result Atualizar(
        string? nome,
        string? descricao,
        string? baseLegal,
        string? origemData,
        SinalizadoresInformados sinalizadores)
    {
        ArgumentNullException.ThrowIfNull(sinalizadores);
        Result<(string Nome, string? Descricao, string? BaseLegal, OrigemDataFase OrigemData)> comuns =
            ValidarCamposComuns(nome, descricao, baseLegal, origemData);
        List<FieldError> erros = [.. comuns.Errors, .. sinalizadores.Recusas(Codigo)];
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        AplicarCampos(comuns.Value, sinalizadores);

        return Result.Success();
    }

    private void AplicarCampos(
        (string Nome, string? Descricao, string? BaseLegal, OrigemDataFase OrigemData) comuns,
        SinalizadoresInformados sinalizadores)
    {
        (Nome, Descricao, BaseLegal, OrigemData) = comuns;
        SinalizadoresDaFase confirmados = sinalizadores.Confirmados();
        if (Sinalizadores != confirmados)
        {
            Sinalizadores = confirmados;
        }
    }

    /// <summary>
    /// Define a unidade sugerida já resolvida no cadastro de unidades pelo handler, ou a retira
    /// quando <paramref name="unidade"/> é <see langword="null"/>.
    /// </summary>
    public void DefinirUnidadeSugerida(UnidadeSugerida? unidade)
    {
        UnidadeSugeridaOrigemId = unidade?.OrigemId;
        UnidadeSugeridaSigla = unidade?.Sigla;
        UnidadeSugeridaNome = unidade?.Nome;
    }

    private static string? ValidarNome(string? nome, List<FieldError> erros)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                FaseCanonicaErrorCodes.NomeObrigatorio, "Nome do tipo de fase é obrigatório.")));
            return null;
        }

        return NormalizarDentroDoLimite(
            nome, "nome", NomeMaxLength, FaseCanonicaErrorCodes.NomeTamanho, "Nome da fase", erros);
    }

    private static string? NormalizarDentroDoLimite(
        string? valor, string campo, int maximo, string codigoDoErro, string rotulo, List<FieldError> erros)
    {
        string? normalizado = string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        if (normalizado is null || normalizado.Length <= maximo)
        {
            return normalizado;
        }

        erros.Add(new(campo, new DomainError(codigoDoErro, $"{rotulo} deve ter no máximo {maximo} caracteres.")));
        return null;
    }

    private static OrigemDataFase ValidarOrigemData(string? origemData, List<FieldError> erros)
    {
        if (!CatalogoDe<OrigemDataFase>.TentarObter(origemData, out OrigemDataFase origem))
        {
            string aceitos = string.Join(", ", CatalogoDe<OrigemDataFase>.Todos().Select(static declarado => declarado.Codigo));
            erros.Add(new("origemData", origemData is null
                ? new DomainError(FaseCanonicaErrorCodes.OrigemDataObrigatoria, "Origem da data da fase é obrigatória.")
                : new DomainError(
                    FaseCanonicaErrorCodes.OrigemDataInvalida,
                    $"Origem da data deve ser uma de: {aceitos}.")));
        }

        return origem;
    }
}
