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
/// conjunto canônico das quatorze fases (<see cref="FaseCanonicaCatalogo"/>).</para>
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
/// reais das quatorze fases são populados via endpoint admin, ato operacional
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
    /// Cria uma nova fase canônica. Valida o código (formato + pertença ao conjunto
    /// canônico das quatorze fases), o nome, o dono típico (token UPPER_SNAKE
    /// obrigatório), a origem da data (token UPPER_SNAKE obrigatório) e as
    /// invariantes de coerência (agrupa etapas ⇒ avaliação; permite complementação ⇒
    /// fase legalmente permitida; resultado definitivo ⇒ produz resultado). A
    /// unicidade do código entre vivos é responsabilidade do handler.
    /// </summary>
    public static Result<FaseCanonica> Criar(
        string codigo,
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
        ArgumentNullException.ThrowIfNull(codigo);

        Result<CodigoFase> codigoResult = CodigoFase.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<FaseCanonica>.Failure(codigoResult.Error!);
        }

        CodigoFase codigoVo = codigoResult.Value!;

        if (!FaseCanonicaCatalogo.EhCanonico(codigoVo.Valor))
        {
            return Result<FaseCanonica>.Failure(new DomainError(
                FaseCanonicaErrorCodes.CodigoForaDoConjuntoCanonico,
                $"Código '{codigoVo.Valor}' não pertence ao conjunto canônico das quatorze fases."));
        }

        Result<CamposResolvidos> camposResult = ValidarComuns(
            codigoVo.Valor, nome, descricao, donoTipico, agrupaEtapas, permiteComplementacao, baseLegal,
            produzResultado, resultadoDefinitivo, coletaInscricao, origemData);
        if (camposResult.IsFailure)
        {
            return Result<FaseCanonica>.Failure(camposResult.Error!);
        }

        var fase = new FaseCanonica { Codigo = codigoVo };
        fase.AplicarCampos(camposResult.Value!);

        return Result<FaseCanonica>.Success(fase);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da fase. O <c>Codigo</c> e o <c>Id</c> são
    /// <b>imutáveis</b> — este método não os recebe. Revalida as invariantes de
    /// coerência contra o código congelado da fase.
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
        Result<CamposResolvidos> camposResult = ValidarComuns(
            Codigo.Valor, nome, descricao, donoTipico, agrupaEtapas, permiteComplementacao, baseLegal,
            produzResultado, resultadoDefinitivo, coletaInscricao, origemData);
        if (camposResult.IsFailure)
        {
            return Result.Failure(camposResult.Error!);
        }

        AplicarCampos(camposResult.Value!);

        return Result.Success();
    }

    private void AplicarCampos(CamposResolvidos campos)
    {
        Nome = campos.Nome;
        Descricao = campos.Descricao;
        DonoTipico = campos.DonoTipico;
        AgrupaEtapas = campos.AgrupaEtapas;
        PermiteComplementacao = campos.PermiteComplementacao;
        BaseLegal = campos.BaseLegal;
        ProduzResultado = campos.ProduzResultado;
        ResultadoDefinitivo = campos.ResultadoDefinitivo;
        ColetaInscricao = campos.ColetaInscricao;
        OrigemData = campos.OrigemData;
    }

    private static Result<CamposResolvidos> ValidarComuns(
        string codigoValor,
        string? nome,
        string? descricao,
        string? donoTipicoToken,
        bool agrupaEtapas,
        bool permiteComplementacao,
        string? baseLegal,
        bool produzResultado,
        bool resultadoDefinitivo,
        bool coletaInscricao,
        string? origemDataToken)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Falha(FaseCanonicaErrorCodes.NomeObrigatorio, "Nome da fase canônica é obrigatório.");
        }

        string nomeNorm = nome.Trim();
        if (nomeNorm.Length > NomeMaxLength)
        {
            return Falha(FaseCanonicaErrorCodes.NomeTamanho,
                $"Nome da fase deve ter no máximo {NomeMaxLength} caracteres.");
        }

        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Falha(FaseCanonicaErrorCodes.DescricaoTamanho,
                $"Descrição da fase deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        if (baseLegal is not null && baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Falha(FaseCanonicaErrorCodes.BaseLegalTamanho,
                $"Base legal da fase deve ter no máximo {BaseLegalMaxLength} caracteres.");
        }

        // DonoTipico — obrigatório, domínio fechado de quatro valores.
        if (string.IsNullOrWhiteSpace(donoTipicoToken))
        {
            return Falha(FaseCanonicaErrorCodes.DonoTipicoObrigatorio, "Dono típico da fase é obrigatório.");
        }

        if (!DonosTipicos.TryAnalisar(donoTipicoToken, out DonoTipico dono))
        {
            return Falha(FaseCanonicaErrorCodes.DonoTipicoInvalido,
                $"Dono típico deve ser um de: {string.Join(", ", DonosTipicos.TokensCanonicos)}.");
        }

        // OrigemData — obrigatória, domínio fechado de dois valores.
        if (string.IsNullOrWhiteSpace(origemDataToken))
        {
            return Falha(FaseCanonicaErrorCodes.OrigemDataObrigatoria, "Origem da data da fase é obrigatória.");
        }

        if (!OrigensDataFase.TryAnalisar(origemDataToken, out OrigemDataFase origemData))
        {
            return Falha(FaseCanonicaErrorCodes.OrigemDataInvalida,
                $"Origem da data deve ser uma de: {string.Join(", ", OrigensDataFase.TokensCanonicos)}.");
        }

        // Coerência: agrupa_etapas verdadeiro só para a fase de avaliação.
        if (agrupaEtapas && !string.Equals(codigoValor, FaseCanonicaCatalogo.CodigoAvaliacao, StringComparison.Ordinal))
        {
            return Falha(FaseCanonicaErrorCodes.AgrupaEtapasApenasAvaliacao,
                "Apenas a fase de avaliação agrupa etapas pontuadas.");
        }

        // Coerência: permite_complementacao verdadeiro só nas fases legalmente permitidas.
        if (permiteComplementacao && !FaseCanonicaCatalogo.PermiteComplementacao(codigoValor))
        {
            return Falha(FaseCanonicaErrorCodes.ComplementacaoApenasFasesPermitidas,
                "Complementação documental só é permitida nas fases de homologação e recursos.");
        }

        // Coerência: resultado definitivo verdadeiro implica produzir resultado (CA-04).
        if (resultadoDefinitivo && !produzResultado)
        {
            return Falha(FaseCanonicaErrorCodes.ResultadoDefinitivoSemProduzirResultado,
                "Uma fase só pode ter resultado definitivo se também produzir resultado.");
        }

        return Result<CamposResolvidos>.Success(new CamposResolvidos(
            nomeNorm,
            NormalizarOpcional(descricao),
            dono,
            agrupaEtapas,
            permiteComplementacao,
            NormalizarOpcional(baseLegal),
            produzResultado,
            resultadoDefinitivo,
            coletaInscricao,
            origemData));
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CamposResolvidos> Falha(string code, string mensagem) =>
        Result<CamposResolvidos>.Failure(new DomainError(code, mensagem));

    private sealed record CamposResolvidos(
        string Nome,
        string? Descricao,
        DonoTipico DonoTipico,
        bool AgrupaEtapas,
        bool PermiteComplementacao,
        string? BaseLegal,
        bool ProduzResultado,
        bool ResultadoDefinitivo,
        bool ColetaInscricao,
        OrigemDataFase OrigemData);
}
