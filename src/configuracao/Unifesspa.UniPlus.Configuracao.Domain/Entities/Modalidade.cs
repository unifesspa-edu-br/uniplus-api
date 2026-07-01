namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Modalidade de concorrência — a entidade mais rica do módulo Configuração
/// (UNI-REQ-0011): modela o sistema de cotas da legislação brasileira de ações
/// afirmativas (Lei 12.711/2012, atual. Lei 14.723/2023). Descreve a natureza
/// jurídica da modalidade, como suas vagas se compõem, para onde vagas ociosas
/// remanejam e o que fazer com o candidato quando indeferido. Dado institucional
/// sem PII (LGPD inaplicável).
/// </summary>
/// <remarks>
/// <para>O <see cref="Codigo"/> (value object <see cref="CodigoModalidade"/>) é a
/// chave natural, único entre modalidades vivas (índice único parcial
/// <c>WHERE is_deleted = false</c>) e <b>imutável</b>: o comando de atualização
/// não o aceita — a cascata de remanejamento e as referências de composição
/// apontam para modalidades por código, e renomear quebraria a integridade
/// referencial intra-banco. A unicidade é checada pelo handler (com proteção de
/// corrida via índice).</para>
/// <para>As invariantes de coerência (natureza↔remanejamento, composição
/// RetiraDe⟺origem, args por regra, ação de indeferimento) moram na factory
/// <see cref="Criar"/>/<see cref="Atualizar"/>. A integridade referencial (todos
/// os códigos referenciados existem vivos; bloqueio de remoção quando referenciada)
/// exige consulta ao banco e mora no handler via repositório.</para>
/// <para>A remoção é sempre soft-delete; nunca bloqueada por snapshot-copy de
/// Seleção (ADR-0061), apenas por referência intra-banco viva (outra modalidade
/// viva que a aponte como origem ou destino/par/fallback).</para>
/// </remarks>
public sealed class Modalidade : SoftDeletableEntity, IAuditableEntity
{
    private const int DescricaoMaxLength = 300;
    private const int BaseLegalMaxLength = 500;
    private const int CodigoReferenciaMaxLength = 60;

    public CodigoModalidade Codigo { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public NaturezaLegal NaturezaLegal { get; private set; }
    public ComposicaoVagas ComposicaoVagas { get; private set; }
    public string? ComposicaoOrigem { get; private set; }
    public RegraRemanejamento? RegraRemanejamento { get; private set; }
    public RemanejamentoArgs RemanejamentoArgs { get; private set; } = RemanejamentoArgs.Vazio;
    public IReadOnlyList<string> CriteriosCumulativos { get; private set; } = [];
    public AcaoQuandoIndeferido? AcaoQuandoIndeferido { get; private set; }
    public string? BaseLegal { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    // EF Core materialization
    private Modalidade()
    {
    }

    /// <summary>
    /// Cria uma nova Modalidade. Valida o código (formato) e todas as invariantes
    /// de coerência de domínio (natureza↔remanejamento, RetiraDe⟺origem, args por
    /// regra, ação de indeferimento). Os enums chegam como tokens textuais
    /// (UPPER_SNAKE): <paramref name="naturezaLegal"/> e <paramref name="composicaoVagas"/>
    /// aplicam default quando ausentes (AMPLA / RESIDUAL_DO_VO);
    /// <paramref name="regraRemanejamento"/> e <paramref name="acaoQuandoIndeferido"/>
    /// são opcionais. A unicidade do código e a integridade referencial dos códigos
    /// citados são responsabilidade do handler.
    /// </summary>
    public static Result<Modalidade> Criar(
        string codigo,
        string? descricao,
        string? naturezaLegal,
        string? composicaoVagas,
        string? composicaoOrigem,
        string? regraRemanejamento,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        IReadOnlyList<string>? criteriosCumulativos,
        string? acaoQuandoIndeferido,
        string? baseLegal)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        Result<CodigoModalidade> codigoResult = CodigoModalidade.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            return Result<Modalidade>.Failure(codigoResult.Error!);
        }

        Result<CamposResolvidos> camposResult = ValidarComuns(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            criteriosCumulativos, acaoQuandoIndeferido, baseLegal);
        if (camposResult.IsFailure)
        {
            return Result<Modalidade>.Failure(camposResult.Error!);
        }

        var modalidade = new Modalidade { Codigo = codigoResult.Value! };
        modalidade.AplicarCampos(camposResult.Value!);

        return Result<Modalidade>.Success(modalidade);
    }

    /// <summary>
    /// Atualiza os atributos editáveis da Modalidade. O <c>Codigo</c> e o <c>Id</c>
    /// são <b>imutáveis</b> — este método não os recebe nem os altera. Revalida
    /// todas as invariantes de coerência de domínio.
    /// </summary>
    public Result Atualizar(
        string? descricao,
        string? naturezaLegal,
        string? composicaoVagas,
        string? composicaoOrigem,
        string? regraRemanejamento,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        IReadOnlyList<string>? criteriosCumulativos,
        string? acaoQuandoIndeferido,
        string? baseLegal)
    {
        Result<CamposResolvidos> camposResult = ValidarComuns(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            criteriosCumulativos, acaoQuandoIndeferido, baseLegal);
        if (camposResult.IsFailure)
        {
            return Result.Failure(camposResult.Error!);
        }

        AplicarCampos(camposResult.Value!);

        return Result.Success();
    }

    private void AplicarCampos(CamposResolvidos campos)
    {
        Descricao = campos.Descricao;
        NaturezaLegal = campos.NaturezaLegal;
        ComposicaoVagas = campos.ComposicaoVagas;
        ComposicaoOrigem = campos.ComposicaoOrigem;
        RegraRemanejamento = campos.RegraRemanejamento;
        RemanejamentoArgs = campos.RemanejamentoArgs;
        CriteriosCumulativos = campos.CriteriosCumulativos;
        AcaoQuandoIndeferido = campos.AcaoQuandoIndeferido;
        BaseLegal = campos.BaseLegal;
    }

    private static Result<CamposResolvidos> ValidarComuns(
        string? descricao,
        string? naturezaLegalToken,
        string? composicaoVagasToken,
        string? composicaoOrigem,
        string? regraRemanejamentoToken,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        IReadOnlyList<string>? criteriosCumulativos,
        string? acaoQuandoIndeferidoToken,
        string? baseLegal)
    {
        if (descricao is not null && descricao.Trim().Length > DescricaoMaxLength)
        {
            return Falha(ModalidadeErrorCodes.DescricaoTamanho,
                $"Descrição da modalidade deve ter no máximo {DescricaoMaxLength} caracteres.");
        }

        if (baseLegal is not null && baseLegal.Trim().Length > BaseLegalMaxLength)
        {
            return Falha(ModalidadeErrorCodes.BaseLegalTamanho,
                $"Base legal da modalidade deve ter no máximo {BaseLegalMaxLength} caracteres.");
        }

        // NaturezaLegal — obrigatória, default AMPLA quando ausente.
        NaturezaLegal natureza;
        if (string.IsNullOrWhiteSpace(naturezaLegalToken))
        {
            natureza = NaturezaLegal.Ampla;
        }
        else if (!NaturezasLegais.TryAnalisar(naturezaLegalToken, out natureza))
        {
            return Falha(ModalidadeErrorCodes.NaturezaInvalida,
                $"Natureza legal deve ser uma de: {string.Join(", ", NaturezasLegais.TokensCanonicos)}.");
        }

        // ComposicaoVagas — obrigatória, default RESIDUAL_DO_VO quando ausente.
        ComposicaoVagas composicao;
        if (string.IsNullOrWhiteSpace(composicaoVagasToken))
        {
            composicao = ComposicaoVagas.ResidualDoVo;
        }
        else if (!ComposicoesVagas.TryAnalisar(composicaoVagasToken, out composicao))
        {
            return Falha(ModalidadeErrorCodes.ComposicaoVagasInvalida,
                $"Composição de vagas deve ser uma de: {string.Join(", ", ComposicoesVagas.TokensCanonicos)}.");
        }

        // RegraRemanejamento — opcional (null quando ausente).
        RegraRemanejamento? regra = null;
        if (!string.IsNullOrWhiteSpace(regraRemanejamentoToken))
        {
            if (!RegrasRemanejamento.TryAnalisar(regraRemanejamentoToken, out RegraRemanejamento regraResolvida))
            {
                return Falha(ModalidadeErrorCodes.RegraRemanejamentoInvalida,
                    $"Regra de remanejamento deve ser uma de: {string.Join(", ", RegrasRemanejamento.TokensCanonicos)}.");
            }

            regra = regraResolvida;
        }

        // AcaoQuandoIndeferido — opcional (null quando ausente); quando informada,
        // deve ser um dos dois tokens (invariante 6).
        AcaoQuandoIndeferido? acao = null;
        if (!string.IsNullOrWhiteSpace(acaoQuandoIndeferidoToken))
        {
            if (!AcoesQuandoIndeferido.TryAnalisar(acaoQuandoIndeferidoToken, out AcaoQuandoIndeferido acaoResolvida))
            {
                return Falha(ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
                    $"Ação quando indeferido deve ser uma de: {string.Join(", ", AcoesQuandoIndeferido.TokensCanonicos)}.");
            }

            acao = acaoResolvida;
        }

        string? origemNorm = NormalizarOpcional(composicaoOrigem);
        if (origemNorm is not null && origemNorm.Length > CodigoReferenciaMaxLength)
        {
            return Falha(ModalidadeErrorCodes.CodigoFormatoInvalido,
                $"Código de origem da composição deve ter no máximo {CodigoReferenciaMaxLength} caracteres.");
        }

        // Invariante 4 — equivalência exata RetiraDe ⟺ ComposicaoOrigem preenchida.
        bool ehRetiraDe = composicao == ComposicaoVagas.RetiraDe;
        if (ehRetiraDe && origemNorm is null)
        {
            return Falha(ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe,
                "Composição RETIRA_DE exige o código de origem (composicao_origem).");
        }

        if (!ehRetiraDe && origemNorm is not null)
        {
            return Falha(ModalidadeErrorCodes.OrigemApenasParaRetiraDe,
                "Código de origem (composicao_origem) só é permitido na composição RETIRA_DE.");
        }

        // Invariante 3 — coerência natureza ↔ regra de remanejamento.
        Result<CamposResolvidos>? coerencia = ValidarCoerenciaNaturezaRemanejamento(natureza, regra);
        if (coerencia is not null)
        {
            return coerencia;
        }

        // Invariante 5 — argumentos exigidos/proibidos por regra.
        RemanejamentoArgs args = RemanejamentoArgs.Criar(
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback);
        Result<CamposResolvidos>? argsCheck = ValidarArgumentosPorRegra(regra, args);
        if (argsCheck is not null)
        {
            return argsCheck;
        }

        IReadOnlyList<string> criterios = NormalizarCriterios(criteriosCumulativos);

        return Result<CamposResolvidos>.Success(new CamposResolvidos(
            NormalizarOpcional(descricao),
            natureza,
            composicao,
            origemNorm,
            regra,
            args,
            criterios,
            acao,
            NormalizarOpcional(baseLegal)));
    }

    private static Result<CamposResolvidos>? ValidarCoerenciaNaturezaRemanejamento(
        NaturezaLegal natureza,
        RegraRemanejamento? regra)
    {
        switch (natureza)
        {
            case NaturezaLegal.CotaReservada when regra != Enums.RegraRemanejamento.SegueCascata:
                return Falha(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Cota reservada exige regra de remanejamento SEGUE_CASCATA.");

            case NaturezaLegal.Ampla when regra is not null:
                return Falha(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Ampla concorrência não admite regra de remanejamento.");

            case NaturezaLegal.Suplementar or NaturezaLegal.OutraModalidade
                when regra is not (Enums.RegraRemanejamento.DestinoUnico or Enums.RegraRemanejamento.Cruzado):
                return Falha(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Modalidade suplementar ou de outra natureza exige regra de remanejamento "
                    + "DESTINO_UNICO ou CRUZADO.");

            default:
                return null;
        }
    }

    private static Result<CamposResolvidos>? ValidarArgumentosPorRegra(
        RegraRemanejamento? regra,
        RemanejamentoArgs args)
    {
        switch (regra)
        {
            case Enums.RegraRemanejamento.DestinoUnico:
                if (args.Destino is null)
                {
                    return Falha(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO exige o argumento 'destino'.");
                }

                if (args.Par is not null || args.Fallback is not null)
                {
                    return Falha(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO não admite os argumentos 'par' e 'fallback'.");
                }

                return null;

            case Enums.RegraRemanejamento.Cruzado:
                if (args.Par is null || args.Fallback is null)
                {
                    return Falha(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra CRUZADO exige os argumentos 'par' e 'fallback'.");
                }

                if (args.Destino is not null)
                {
                    return Falha(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra CRUZADO não admite o argumento 'destino'.");
                }

                return null;

            // SegueCascata ou sem regra: nenhum argumento é permitido.
            default:
                return args.TemAlgum
                    ? Falha(ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Nenhum argumento de remanejamento é admitido para esta regra.")
                    : null;
        }
    }

    private static IReadOnlyList<string> NormalizarCriterios(IReadOnlyList<string>? criterios)
    {
        if (criterios is null || criterios.Count == 0)
        {
            return [];
        }

        return [.. criterios
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())];
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static Result<CamposResolvidos> Falha(string code, string mensagem) =>
        Result<CamposResolvidos>.Failure(new DomainError(code, mensagem));

    private sealed record CamposResolvidos(
        string? Descricao,
        NaturezaLegal NaturezaLegal,
        ComposicaoVagas ComposicaoVagas,
        string? ComposicaoOrigem,
        RegraRemanejamento? RegraRemanejamento,
        RemanejamentoArgs RemanejamentoArgs,
        IReadOnlyList<string> CriteriosCumulativos,
        AcaoQuandoIndeferido? AcaoQuandoIndeferido,
        string? BaseLegal);
}
