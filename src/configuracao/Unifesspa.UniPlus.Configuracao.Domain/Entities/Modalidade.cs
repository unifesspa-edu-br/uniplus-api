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
/// viva que a aponte como origem ou destino/par/fallback) — ou por ser uma
/// modalidade do catálogo legal fixo.</para>
/// <para>As onze modalidades do <b>catálogo legal fixo</b>
/// (<see cref="CodigoModalidade.CodigosLegaisFixos"/>) são cadastro só na forma: a
/// estrutura de vagas de cada uma vem de norma — a Lei 12.711/2012 (red. Lei
/// 14.723/2023) ou a resolução institucional que reserva a vaga de pessoa com
/// deficiência —, não da universidade. Nelas <see cref="Atualizar"/> aceita apenas
/// <see cref="Descricao"/> e <see cref="BaseLegal"/>; os handlers recusam removê-las e recusam cadastrar os
/// seus códigos. Alterar a estrutura exige mudança no seed e migração — o mesmo canal
/// que as criou.</para>
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
    /// Cria uma nova Modalidade, acumulando toda violação independente em vez de
    /// parar na primeira. Valida o código (formato) e todas as invariantes de
    /// coerência de domínio (natureza↔remanejamento, RetiraDe⟺origem, args por
    /// regra, ação de indeferimento). Os enums chegam como tokens textuais
    /// (UPPER_SNAKE): <paramref name="naturezaLegal"/> e <paramref name="composicaoVagas"/>
    /// aplicam default quando ausentes (AMPLA / RESIDUAL_DO_VO);
    /// <paramref name="regraRemanejamento"/> e <paramref name="acaoQuandoIndeferido"/>
    /// são opcionais.
    /// </summary>
    /// <remarks>
    /// A factory <b>aceita</b> os onze códigos do catálogo legal fixo — recusá-los é
    /// papel do cadastro (handler), não do domínio: o mesmo código válido pode
    /// legitimamente chegar aqui por outra via que não o endpoint de criação (ex.:
    /// reconstrução administrativa a partir do estado semeado). A unicidade do
    /// código e a integridade referencial dos códigos citados também são
    /// responsabilidade do handler, pois exigem consulta ao banco.
    /// </remarks>
    public static Result<Modalidade> Criar(
        string? codigo,
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
        List<FieldError> erros = [];

        Result<CodigoModalidade> codigoResult = CodigoModalidade.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }

        Result<CamposResolvidos> camposResult = ValidarCampos(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            criteriosCumulativos, acaoQuandoIndeferido, baseLegal);
        if (camposResult.IsFailure)
        {
            erros.AddRange(camposResult.Errors);
        }

        if (erros.Count > 0)
        {
            return Result<Modalidade>.ValidationFailure(erros);
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
    /// <remarks>
    /// Numa modalidade do catálogo legal fixo (<see cref="ValueObjects.CodigoModalidade.EhLegalFixa"/>)
    /// só <c>Descricao</c> e <c>BaseLegal</c> são editáveis; divergência em qualquer outro
    /// campo retorna <c>EstruturaProtegidaNaoEditavel</c>. A guarda roda depois de resolver
    /// os campos e <b>antes</b> das invariantes cruzadas: assim uma tentativa de alterar a
    /// natureza de uma cota federal responde "esta modalidade não se edita" em vez de
    /// "corrija a coerência do payload", que sugeriria que a edição seria possível.
    /// </remarks>
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
        (List<FieldError> erros, _, _, _, CamposResolvidos campos) = ValidarCamposIndependentes(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            criteriosCumulativos, acaoQuandoIndeferido, baseLegal);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        if (Codigo.EhLegalFixa && EstruturaDivergeDe(campos))
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel,
                "Esta modalidade pertence ao catálogo legal fixo e admite alteração apenas de "
                + "descrição e base legal — natureza, composição, remanejamento, critérios e ação "
                + "no indeferimento são fixados em lei."));
        }

        Result coerencia = ValidarCoerenciaCruzada(campos);
        if (coerencia.IsFailure)
        {
            return coerencia;
        }

        AplicarCampos(campos);

        return Result.Success();
    }

    /// <summary>
    /// Indica se <paramref name="campos"/> altera algum atributo fora da allowlist de
    /// edição do catálogo legal fixo. A allowlist enumera o que <b>pode</b> mudar
    /// (descrição e base legal) — assim um atributo novo do agregado nasce protegido por
    /// omissão, em vez de desprotegido.
    /// </summary>
    private bool EstruturaDivergeDe(CamposResolvidos campos) =>
        campos.NaturezaLegal != NaturezaLegal
        || campos.ComposicaoVagas != ComposicaoVagas
        || !string.Equals(campos.ComposicaoOrigem, ComposicaoOrigem, StringComparison.Ordinal)
        || campos.RegraRemanejamento != RegraRemanejamento
        || campos.RemanejamentoArgs != RemanejamentoArgs
        || !campos.CriteriosCumulativos.SequenceEqual(CriteriosCumulativos, StringComparer.Ordinal)
        || campos.AcaoQuandoIndeferido != AcaoQuandoIndeferido;

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

    /// <summary>
    /// Valida e normaliza os campos editáveis independentes de qualquer coerência
    /// cruzada, acumulando toda violação em vez de parar na primeira — tokens dos
    /// enums, normalização de texto e tamanhos. Também devolve, junto do lote de
    /// erros, se cada token do qual alguma invariante cruzada depende foi
    /// reconhecido (<paramref name="naturezaLegalToken"/> → natureza,
    /// <paramref name="composicaoVagasToken"/> → composição,
    /// <paramref name="regraRemanejamentoToken"/> → regra): quando o lote de erros
    /// está vazio, os três sinalizadores são sempre verdadeiros, por construção.
    /// Sem I/O — separada de <see cref="ValidarCoerenciaCruzada"/> porque
    /// <see cref="Atualizar"/> precisa intercalar a guarda do catálogo legal fixo
    /// entre as duas.
    /// </summary>
    private static (List<FieldError> Erros, bool NaturezaOk, bool ComposicaoOk, bool RegraOk, CamposResolvidos Campos)
        ValidarCamposIndependentes(
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
        List<FieldError> erros = [];

        string? descricaoNorm = NormalizarOpcional(descricao);
        if (descricaoNorm is not null && descricaoNorm.Length > DescricaoMaxLength)
        {
            erros.Add(new("descricao", new DomainError(
                ModalidadeErrorCodes.DescricaoTamanho,
                $"Descrição da modalidade deve ter no máximo {DescricaoMaxLength} caracteres.")));
        }

        string? baseLegalNorm = NormalizarOpcional(baseLegal);
        if (baseLegalNorm is not null && baseLegalNorm.Length > BaseLegalMaxLength)
        {
            erros.Add(new("baseLegal", new DomainError(
                ModalidadeErrorCodes.BaseLegalTamanho,
                $"Base legal da modalidade deve ter no máximo {BaseLegalMaxLength} caracteres.")));
        }

        // NaturezaLegal — obrigatória, default AMPLA quando ausente.
        bool naturezaOk = true;
        NaturezaLegal natureza = NaturezaLegal.Ampla;
        if (!string.IsNullOrWhiteSpace(naturezaLegalToken) && !NaturezasLegais.TryAnalisar(naturezaLegalToken, out natureza))
        {
            naturezaOk = false;
            erros.Add(new("naturezaLegal", new DomainError(
                ModalidadeErrorCodes.NaturezaInvalida,
                $"Natureza legal deve ser uma de: {string.Join(", ", NaturezasLegais.TokensCanonicos)}.")));
        }

        // ComposicaoVagas — obrigatória, default RESIDUAL_DO_VO quando ausente.
        bool composicaoOk = true;
        ComposicaoVagas composicao = ComposicaoVagas.ResidualDoVo;
        if (!string.IsNullOrWhiteSpace(composicaoVagasToken) && !ComposicoesVagas.TryAnalisar(composicaoVagasToken, out composicao))
        {
            composicaoOk = false;
            erros.Add(new("composicaoVagas", new DomainError(
                ModalidadeErrorCodes.ComposicaoVagasInvalida,
                $"Composição de vagas deve ser uma de: {string.Join(", ", ComposicoesVagas.TokensCanonicos)}.")));
        }

        // RegraRemanejamento — opcional (null quando ausente).
        bool regraOk = true;
        RegraRemanejamento? regra = null;
        if (!string.IsNullOrWhiteSpace(regraRemanejamentoToken))
        {
            if (RegrasRemanejamento.TryAnalisar(regraRemanejamentoToken, out RegraRemanejamento regraResolvida))
            {
                regra = regraResolvida;
            }
            else
            {
                regraOk = false;
                erros.Add(new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.RegraRemanejamentoInvalida,
                    $"Regra de remanejamento deve ser uma de: {string.Join(", ", RegrasRemanejamento.TokensCanonicos)}.")));
            }
        }

        // AcaoQuandoIndeferido — opcional (null quando ausente); quando informada,
        // deve ser um dos dois tokens (invariante 6). Nada depende dela — sem gate.
        AcaoQuandoIndeferido? acao = null;
        if (!string.IsNullOrWhiteSpace(acaoQuandoIndeferidoToken))
        {
            if (AcoesQuandoIndeferido.TryAnalisar(acaoQuandoIndeferidoToken, out AcaoQuandoIndeferido acaoResolvida))
            {
                acao = acaoResolvida;
            }
            else
            {
                erros.Add(new("acaoQuandoIndeferido", new DomainError(
                    ModalidadeErrorCodes.AcaoIndeferimentoInvalida,
                    $"Ação quando indeferido deve ser uma de: {string.Join(", ", AcoesQuandoIndeferido.TokensCanonicos)}.")));
            }
        }

        string? origemNorm = NormalizarOpcional(composicaoOrigem);
        if (origemNorm is not null && origemNorm.Length > CodigoReferenciaMaxLength)
        {
            erros.Add(new("composicaoOrigem", new DomainError(
                ModalidadeErrorCodes.CodigoFormatoInvalido,
                $"Código de origem da composição deve ter no máximo {CodigoReferenciaMaxLength} caracteres.")));
        }

        RemanejamentoArgs args = RemanejamentoArgs.Criar(
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback);

        IReadOnlyList<string> criterios = NormalizarCriterios(criteriosCumulativos);

        var campos = new CamposResolvidos(
            descricaoNorm, natureza, composicao, origemNorm, regra, args, criterios, acao, baseLegalNorm);

        return (erros, naturezaOk, composicaoOk, regraOk, campos);
    }

    /// <summary>
    /// Valida só os campos independentes de coerência cruzada (parse dos enums,
    /// tamanhos), sem I/O — para o handler de atualização falhar rápido antes do
    /// fetch. <b>Não</b> inclui as três invariantes cruzadas nem a guarda do
    /// catálogo legal fixo: a guarda depende do <c>Codigo</c> já persistido, e as
    /// coerências têm de vir depois dela — reportá-las aqui, antes do fetch,
    /// preemptiria <c>EstruturaProtegidaNaoEditavel</c> com um erro de coerência
    /// para um payload que também é internamente incoerente. Só
    /// <see cref="Atualizar"/>, depois do fetch, avalia guarda e coerência na
    /// ordem certa.
    /// </summary>
    public static Result ValidarCamposDoPayload(
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
        (List<FieldError> erros, _, _, _, _) = ValidarCamposIndependentes(
            descricao, naturezaLegalToken, composicaoVagasToken, composicaoOrigem, regraRemanejamentoToken,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            criteriosCumulativos, acaoQuandoIndeferidoToken, baseLegal);

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    /// <summary>
    /// Valida os campos editáveis por completo, acumulando toda violação
    /// independente — os campos que não cruzam entre si (<see cref="ValidarCamposIndependentes"/>)
    /// e as três invariantes cruzadas (RetiraDe⟺origem, natureza↔regra, args por
    /// regra), cada uma só avaliada quando os tokens de que ela depende já foram
    /// reconhecidos (senão o erro reportado seria derivado de um token inválido,
    /// não uma incoerência independente — ex.: reportar "SEGUE_CASCATA exigido"
    /// sobre uma <c>NaturezaLegal</c> que nem chegou a resolver para um valor de
    /// domínio). Sem I/O — existe também para o handler de atualização falhar
    /// rápido antes de buscar a modalidade por Id.
    /// </summary>
    public static Result<CamposResolvidos> ValidarCampos(
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
        (List<FieldError> erros, bool naturezaOk, bool composicaoOk, bool regraOk, CamposResolvidos campos) =
            ValidarCamposIndependentes(
                descricao, naturezaLegalToken, composicaoVagasToken, composicaoOrigem, regraRemanejamentoToken,
                remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
                criteriosCumulativos, acaoQuandoIndeferidoToken, baseLegal);

        // Invariante 4 — equivalência exata RetiraDe ⟺ origem preenchida. A
        // presença de origem independe do tamanho já sinalizado acima; só depende
        // de ComposicaoVagas ter sido reconhecida.
        if (composicaoOk)
        {
            bool ehRetiraDe = campos.ComposicaoVagas == ComposicaoVagas.RetiraDe;
            if (ehRetiraDe && campos.ComposicaoOrigem is null)
            {
                erros.Add(new("composicaoOrigem", new DomainError(
                    ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe,
                    "Composição RETIRA_DE exige o código de origem (composicao_origem).")));
            }
            else if (!ehRetiraDe && campos.ComposicaoOrigem is not null)
            {
                erros.Add(new("composicaoOrigem", new DomainError(
                    ModalidadeErrorCodes.OrigemApenasParaRetiraDe,
                    "Código de origem (composicao_origem) só é permitido na composição RETIRA_DE.")));
            }
        }

        // Invariante 3 — coerência natureza ↔ regra de remanejamento.
        if (naturezaOk && regraOk)
        {
            FieldError? incoerencia = ValidarCoerenciaNaturezaRemanejamento(campos.NaturezaLegal, campos.RegraRemanejamento);
            if (incoerencia is not null)
            {
                erros.Add(incoerencia);
            }
        }

        // Invariante 5 — argumentos exigidos/proibidos por regra.
        if (regraOk)
        {
            FieldError? argumentoInvalido = ValidarArgumentosPorRegra(campos.RegraRemanejamento, campos.RemanejamentoArgs);
            if (argumentoInvalido is not null)
            {
                erros.Add(argumentoInvalido);
            }
        }

        return erros.Count == 0
            ? Result<CamposResolvidos>.Success(campos)
            : Result<CamposResolvidos>.ValidationFailure(erros);
    }

    /// <summary>
    /// As três invariantes que cruzam campos já resolvidos e confirmados válidos
    /// individualmente (RetiraDe⟺origem, natureza↔regra, args por regra),
    /// acumulando toda violação independente. Diferente de <see cref="ValidarCampos"/>,
    /// não precisa de sinalizador de gate: só é chamada por <see cref="Atualizar"/>
    /// depois que <see cref="ValidarCamposIndependentes"/> já devolveu zero erros —
    /// os três tokens dos quais estas invariantes dependem já estão, por
    /// construção, reconhecidos.
    /// </summary>
    private static Result ValidarCoerenciaCruzada(CamposResolvidos campos)
    {
        List<FieldError> erros = [];

        bool ehRetiraDe = campos.ComposicaoVagas == ComposicaoVagas.RetiraDe;
        if (ehRetiraDe && campos.ComposicaoOrigem is null)
        {
            erros.Add(new("composicaoOrigem", new DomainError(
                ModalidadeErrorCodes.OrigemObrigatoriaParaRetiraDe,
                "Composição RETIRA_DE exige o código de origem (composicao_origem).")));
        }
        else if (!ehRetiraDe && campos.ComposicaoOrigem is not null)
        {
            erros.Add(new("composicaoOrigem", new DomainError(
                ModalidadeErrorCodes.OrigemApenasParaRetiraDe,
                "Código de origem (composicao_origem) só é permitido na composição RETIRA_DE.")));
        }

        FieldError? incoerencia = ValidarCoerenciaNaturezaRemanejamento(campos.NaturezaLegal, campos.RegraRemanejamento);
        if (incoerencia is not null)
        {
            erros.Add(incoerencia);
        }

        FieldError? argumentoInvalido = ValidarArgumentosPorRegra(campos.RegraRemanejamento, campos.RemanejamentoArgs);
        if (argumentoInvalido is not null)
        {
            erros.Add(argumentoInvalido);
        }

        return erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
    }

    private static FieldError? ValidarCoerenciaNaturezaRemanejamento(
        NaturezaLegal natureza,
        RegraRemanejamento? regra)
    {
        switch (natureza)
        {
            case NaturezaLegal.CotaReservada when regra != Enums.RegraRemanejamento.SegueCascata:
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Cota reservada exige regra de remanejamento SEGUE_CASCATA."));

            case NaturezaLegal.Ampla when regra is not null:
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Ampla concorrência não admite regra de remanejamento."));

            case NaturezaLegal.Suplementar or NaturezaLegal.OutraModalidade
                when regra is not (Enums.RegraRemanejamento.DestinoUnico or Enums.RegraRemanejamento.Cruzado):
                return new("regraRemanejamento", new DomainError(
                    ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente,
                    "Modalidade suplementar ou de outra natureza exige regra de remanejamento "
                    + "DESTINO_UNICO ou CRUZADO."));

            default:
                return null;
        }
    }

    private static FieldError? ValidarArgumentosPorRegra(
        RegraRemanejamento? regra,
        RemanejamentoArgs args)
    {
        switch (regra)
        {
            case Enums.RegraRemanejamento.DestinoUnico:
                if (args.Destino is null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO exige o argumento 'destino'."));
                }

                if (args.Par is not null || args.Fallback is not null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra DESTINO_UNICO não admite os argumentos 'par' e 'fallback'."));
                }

                return null;

            case Enums.RegraRemanejamento.Cruzado:
                if (args.Par is null || args.Fallback is null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra CRUZADO exige os argumentos 'par' e 'fallback'."));
                }

                if (args.Destino is not null)
                {
                    return new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Regra CRUZADO não admite o argumento 'destino'."));
                }

                return null;

            // SegueCascata ou sem regra: nenhum argumento é permitido.
            default:
                return args.TemAlgum
                    ? new("regraRemanejamento", new DomainError(
                        ModalidadeErrorCodes.ArgumentoRemanejamentoObrigatorio,
                        "Nenhum argumento de remanejamento é admitido para esta regra."))
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

    public sealed record CamposResolvidos(
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
