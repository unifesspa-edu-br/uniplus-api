namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Fase canônica (UNI-REQ-0064): nomeia, em domínio fechado, os momentos do ciclo
/// de vida de um processo seletivo (inscrição, homologação, avaliação, recursos,
/// resultado, matrícula…). Dado institucional de referência sem PII (LGPD
/// inaplicável). Não é Etapa — a Etapa pontuada pertence a outro requisito; aqui o
/// sinalizador <see cref="AgrupaEtapas"/> apenas marca qual fase as agrupa.
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoFase"/>) é a chave
/// natural, único entre fases vivas (índice único parcial <c>WHERE is_deleted =
/// false</c>) e <b>imutável</b>: o comando de atualização não o aceita — o
/// vocabulário canônico é fixo. Além do formato, o código deve pertencer ao
/// conjunto canônico de fases (<see cref="FaseCanonicaCatalogo"/>).</para>
/// <para>As invariantes de coerência (<see cref="AgrupaEtapas"/> só para a fase de
/// avaliação; <see cref="PermiteComplementacao"/> só nas fases legalmente permitidas;
/// <see cref="ResultadoDefinitivo"/> só quando <see cref="ProduzResultado"/>) moram
/// na factory <see cref="Criar"/>/<see cref="Atualizar"/>, revalidadas contra o
/// código imutável. A remoção é sempre soft-delete; nunca bloqueada — o único
/// consumo é por snapshot-copy desacoplado no Módulo Seleção (ADR-0061), e não há
/// FK intra-banco apontando para esta entidade.</para>
/// <para><see cref="ProduzResultado"/>, <see cref="ResultadoDefinitivo"/> e
/// <see cref="ColetaInscricao"/> decidem o piso mínimo e a admissibilidade de
/// recurso do cronograma de um processo (Módulo Seleção); <see cref="OrigemData"/>
/// decide se a janela da fase é obrigatória (<c>PROPRIA</c>) ou opcional
/// (<c>DELEGADA</c>). Cadastro <b>100% CRUD-administrado, sem seed</b> — os valores
/// reais das fases são populados via endpoint admin, ato operacional
/// pós-deploy, não bloqueio de código.</para>
/// </remarks>
public sealed class FaseCanonica : SoftDeletableEntity, IAuditableEntity
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 300;
    private const int BaseLegalMaxLength = 500;

    public CodigoFase Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public DonoTipico DonoTipico { get; private set; }
    public bool AgrupaEtapas { get; private set; }
    public bool PermiteComplementacao { get; private set; }
    public string? BaseLegal { get; private set; }

    /// <summary>Havendo vagas ofertadas, o cronograma precisa de ao menos uma fase com este sinalizador verdadeiro.</summary>
    public bool ProduzResultado { get; private set; }

    /// <summary>Verdadeiro quando o resultado produzido pela fase não admite recurso. Implica <see cref="ProduzResultado"/>.</summary>
    public bool ResultadoDefinitivo { get; private set; }

    /// <summary>Verdadeiro quando a fase coleta inscrições — decide o piso mínimo quando a origem dos candidatos é inscrição própria.</summary>
    public bool ColetaInscricao { get; private set; }

    /// <summary>Quem controla a data da fase: <see cref="OrigemDataFase.Propria"/> (janela obrigatória) ou <see cref="OrigemDataFase.Delegada"/> (janela opcional).</summary>
    public OrigemDataFase OrigemData { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private FaseCanonica()
    {
    }

    /// <summary>
    /// Valida o código: formato fechado (<see cref="CodigoFase.Criar"/>) e pertença
    /// ao conjunto canônico de fases. Sem mutar nada — existe para
    /// <see cref="Criar"/> decidir se as coerências que dependem do código (
    /// <see cref="AgrupaEtapas"/>, <see cref="PermiteComplementacao"/>) fazem
    /// sentido de avaliar, e para o handler consultar unicidade só com um código
    /// já confiável.
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
    /// Valida e normaliza os sete campos editáveis que <b>não</b> dependem do
    /// código da fase (nome, descrição, dono típico, base legal, origem da data e
    /// a coerência que exige produzir resultado quando o resultado é definitivo),
    /// acumulando toda violação independente em vez de parar na primeira. Sem
    /// mutar nada e sem depender de I/O — existe para o handler de atualização
    /// falhar rápido antes de buscar a fase por Id, quando a causa da violação já
    /// é determinável só pelo payload.
    /// </summary>
    public static Result<(string Nome, string? Descricao, DonoTipico DonoTipico, string? BaseLegal,
        bool ProduzResultado, bool ResultadoDefinitivo, OrigemDataFase OrigemData)> ValidarCamposComuns(
        string? nome, string? descricao, string? donoTipico, string? baseLegal,
        bool produzResultado, bool resultadoDefinitivo, string? origemData)
    {
        List<FieldError> erros = [];

        string? nomeNormalizado = null;
        if (string.IsNullOrWhiteSpace(nome))
        {
            erros.Add(new("nome", new DomainError(
                FaseCanonicaErrorCodes.NomeObrigatorio, "Nome da fase canônica é obrigatório.")));
        }
        else
        {
            nomeNormalizado = nome.Trim();
            if (nomeNormalizado.Length > NomeMaxLength)
            {
                erros.Add(new("nome", new DomainError(
                    FaseCanonicaErrorCodes.NomeTamanho,
                    $"Nome da fase deve ter no máximo {NomeMaxLength} caracteres.")));
                nomeNormalizado = null;
            }
        }

        string? descricaoNormalizada = NormalizarOpcional(descricao);
        if (descricaoNormalizada is not null && descricaoNormalizada.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                FaseCanonicaErrorCodes.DescricaoTamanho,
                $"Descrição da fase deve ter no máximo {DescricaoMaxLength} caracteres.")));
            descricaoNormalizada = null;
        }

        DonoTipico dono = DonoTipico.Nenhum;
        if (string.IsNullOrWhiteSpace(donoTipico))
        {
            erros.Add(new("donoTipico", new DomainError(
                FaseCanonicaErrorCodes.DonoTipicoObrigatorio, "Dono típico da fase é obrigatório.")));
        }
        else if (!DonosTipicos.TryAnalisar(donoTipico, out dono))
        {
            erros.Add(new("donoTipico", new DomainError(
                FaseCanonicaErrorCodes.DonoTipicoInvalido,
                $"Dono típico deve ser um de: {string.Join(", ", DonosTipicos.TokensCanonicos)}.")));
        }

        string? baseLegalNormalizada = NormalizarOpcional(baseLegal);
        if (baseLegalNormalizada is not null && baseLegalNormalizada.Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                FaseCanonicaErrorCodes.BaseLegalTamanho,
                $"Base legal da fase deve ter no máximo {BaseLegalMaxLength} caracteres.")));
            baseLegalNormalizada = null;
        }

        OrigemDataFase origem = OrigemDataFase.Nenhuma;
        if (string.IsNullOrWhiteSpace(origemData))
        {
            erros.Add(new("origemData", new DomainError(
                FaseCanonicaErrorCodes.OrigemDataObrigatoria, "Origem da data da fase é obrigatória.")));
        }
        else if (!OrigensDataFase.TryAnalisar(origemData, out origem))
        {
            erros.Add(new("origemData", new DomainError(
                FaseCanonicaErrorCodes.OrigemDataInvalida,
                $"Origem da data deve ser uma de: {string.Join(", ", OrigensDataFase.TokensCanonicos)}.")));
        }

        // Resultado definitivo verdadeiro implica produzir resultado — não depende
        // do código, só dos dois booleanos, sempre presentes.
        if (resultadoDefinitivo && !produzResultado)
        {
            erros.Add(new("resultadoDefinitivo", new DomainError(
                FaseCanonicaErrorCodes.ResultadoDefinitivoSemProduzirResultado,
                "Uma fase só pode ter resultado definitivo se também produzir resultado.")));
        }

        if (erros.Count > 0)
        {
            return Result<(string, string?, DonoTipico, string?, bool, bool, OrigemDataFase)>.ValidationFailure(erros);
        }

        return Result<(string, string?, DonoTipico, string?, bool, bool, OrigemDataFase)>.Success(
            (nomeNormalizado!, descricaoNormalizada, dono, baseLegalNormalizada, produzResultado, resultadoDefinitivo, origem));
    }

    /// <summary>
    /// Valida as duas coerências que dependem do código da fase — <see cref="AgrupaEtapas"/>
    /// só para a fase de avaliação, <see cref="PermiteComplementacao"/> só nas fases
    /// legalmente permitidas — acumulando as duas violações independentes. Só faz
    /// sentido avaliar contra um <paramref name="codigoValor"/> já confiável (formato
    /// válido e pertencente ao conjunto canônico); por isso <see cref="Criar"/> só a
    /// chama quando <see cref="ValidarCodigo"/> teve sucesso.
    /// </summary>
    public static Result ValidarCoerenciaDeCodigo(string codigoValor, bool agrupaEtapas, bool permiteComplementacao)
    {
        List<FieldError> erros = [];

        if (agrupaEtapas && !string.Equals(codigoValor, FaseCanonicaCatalogo.CodigoAvaliacao, StringComparison.Ordinal))
        {
            erros.Add(new("agrupaEtapas", new DomainError(
                FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
                "Apenas a fase de avaliação agrupa etapas pontuadas.")));
        }

        if (permiteComplementacao && !FaseCanonicaCatalogo.PermiteComplementacao(codigoValor))
        {
            erros.Add(new("permiteComplementacao", new DomainError(
                FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
                "Complementação documental só é permitida nas fases de homologação e recursos.")));
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Cria uma nova fase canônica. Valida o código (formato + pertença ao conjunto
    /// canônico), os sete campos comuns e — só quando o código é válido — as duas
    /// coerências que dependem dele, acumulando toda violação no mesmo lote. A
    /// unicidade do código entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<FaseCanonica> Criar(
        string? codigo,
        string? nome,
        string? descricao,
        string? donoTipico,
        bool agrupaEtapas,
        bool permiteComplementacao,
        string? baseLegal,
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        string? origemData)
    {
        List<FieldError> erros = [];

        Result<CodigoFase> codigoResult = ValidarCodigo(codigo);
        if (codigoResult.IsFailure)
        {
            erros.AddRange(codigoResult.Errors);
        }

        Result<(string Nome, string? Descricao, DonoTipico DonoTipico, string? BaseLegal,
            bool ProduzResultado, bool ResultadoDefinitivo, OrigemDataFase OrigemData)> comunsResult =
            ValidarCamposComuns(nome, descricao, donoTipico, baseLegal, produzResultado, resultadoDefinitivo, origemData);
        if (comunsResult.IsFailure)
        {
            erros.AddRange(comunsResult.Errors);
        }

        // As coerências dependentes do código só fazem sentido contra um código já
        // confiável — se o código falhou, relatar "AgrupaEtapasApenasAvaliacao" sobre
        // um valor malformado seria um segundo erro derivado do primeiro, não uma
        // violação independente.
        if (codigoResult.IsSuccess)
        {
            Result coerencia = ValidarCoerenciaDeCodigo(codigoResult.Value!.Valor, agrupaEtapas, permiteComplementacao);
            if (coerencia.IsFailure)
            {
                erros.AddRange(coerencia.Errors);
            }
        }

        if (erros.Count > 0)
        {
            return Result<FaseCanonica>.ValidationFailure(erros);
        }

        var fase = new FaseCanonica { Codigo = codigoResult.Value! };
        fase.AplicarCampos(
            comunsResult.Value.Nome, comunsResult.Value.Descricao, comunsResult.Value.DonoTipico,
            agrupaEtapas, permiteComplementacao, comunsResult.Value.BaseLegal, comunsResult.Value.ProduzResultado,
            comunsResult.Value.ResultadoDefinitivo, coletaInscricao, comunsResult.Value.OrigemData);

        return Result<FaseCanonica>.Success(fase);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da fase. O <c>Codigo</c> e o <c>Id</c> são
    /// <b>imutáveis</b> — este método não os recebe. Revalida os sete campos comuns
    /// e as duas coerências contra o código congelado da fase, acumulando toda
    /// violação no mesmo lote — é a rede de segurança que cobre as duas regras que
    /// o pré-check do handler não consegue avaliar sem o código persistido.
    /// </summary>
    public Result Atualizar(
        string? nome,
        string? descricao,
        string? donoTipico,
        bool agrupaEtapas,
        bool permiteComplementacao,
        string? baseLegal,
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        string? origemData)
    {
        List<FieldError> erros = [];

        Result<(string Nome, string? Descricao, DonoTipico DonoTipico, string? BaseLegal,
            bool ProduzResultado, bool ResultadoDefinitivo, OrigemDataFase OrigemData)> comunsResult =
            ValidarCamposComuns(nome, descricao, donoTipico, baseLegal, produzResultado, resultadoDefinitivo, origemData);
        if (comunsResult.IsFailure)
        {
            erros.AddRange(comunsResult.Errors);
        }

        Result coerencia = ValidarCoerenciaDeCodigo(Codigo.Valor, agrupaEtapas, permiteComplementacao);
        if (coerencia.IsFailure)
        {
            erros.AddRange(coerencia.Errors);
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        AplicarCampos(
            comunsResult.Value.Nome, comunsResult.Value.Descricao, comunsResult.Value.DonoTipico,
            agrupaEtapas, permiteComplementacao, comunsResult.Value.BaseLegal, comunsResult.Value.ProduzResultado,
            comunsResult.Value.ResultadoDefinitivo, coletaInscricao, comunsResult.Value.OrigemData);

        return Result.Success();
    }

    private void AplicarCampos(
        string nome, string? descricao, DonoTipico donoTipico, bool agrupaEtapas, bool permiteComplementacao,
        string? baseLegal, bool produzResultado, bool resultadoDefinitivo, bool coletaInscricao, OrigemDataFase origemData)
    {
        Nome = nome;
        Descricao = descricao;
        DonoTipico = donoTipico;
        AgrupaEtapas = agrupaEtapas;
        PermiteComplementacao = permiteComplementacao;
        BaseLegal = baseLegal;
        ProduzResultado = produzResultado;
        ResultadoDefinitivo = resultadoDefinitivo;
        ColetaInscricao = coletaInscricao;
        OrigemData = origemData;
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
