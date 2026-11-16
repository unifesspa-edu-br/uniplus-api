namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

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
/// RetiradaDeOutraModalidade⟺origem, args por regra, ação de indeferimento) moram na factory
/// <see cref="Criar"/>/<see cref="Atualizar"/>. A integridade referencial (todos
/// os códigos referenciados existem vivos; bloqueio de remoção quando referenciada)
/// exige consulta ao banco e mora no handler via repositório.</para>
/// <para>A remoção é sempre soft-delete; nunca bloqueada por snapshot-copy de
/// Seleção (ADR-0061), apenas por referência intra-banco viva (outra modalidade
/// viva que a aponte como origem ou destino/par/fallback) — ou por ser uma
/// modalidade do catálogo legal fixo.</para>
/// <para>As treze modalidades do <b>catálogo legal fixo</b>
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
    public CodigoModalidade Codigo { get; private set; } = null!;
    public string? Descricao { get; private set; }
    public NaturezaLegal NaturezaLegal { get; private set; }
    public ComposicaoVagas ComposicaoVagas { get; private set; }
    public string? ComposicaoOrigem { get; private set; }
    public RegraRemanejamento? RegraRemanejamento { get; private set; }
    public RemanejamentoArgs RemanejamentoArgs { get; private set; } = RemanejamentoArgs.Vazio;
    /// <summary>
    /// Quem concorre à modalidade: o predicado sobre os fatos do candidato que o enquadra nela
    /// (ADR-0144). A ampla concorrência é incondicional, sem cláusula; a cota da lei exige a opção
    /// pelas cotas em toda cláusula, e a ação afirmativa exige não tê-la feito (UNI-REQ-0142).
    /// </summary>
    public PredicadoDnf RegraDeAplicabilidade { get; private set; } = null!;
    /// <summary>
    /// O que fazer com o candidato quando a comprovação da reserva é indeferida.
    /// <see langword="null"/> declara que esta modalidade não reclassifica — quem responde
    /// pelo destino é a consequência de indeferimento da exigência documental. Ver
    /// <see cref="Enums.AcaoQuandoIndeferido"/>.
    /// </summary>
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
    /// coerência de domínio (natureza↔remanejamento, RetiradaDeOutraModalidade⟺origem, args por
    /// regra, ação de indeferimento). Os enums chegam como tokens textuais
    /// (UPPER_SNAKE): <paramref name="naturezaLegal"/> e <paramref name="composicaoVagas"/>
    /// aplicam default quando ausentes (AMPLA / RESTANTE_DAS_VAGAS_OFERTADAS);
    /// <paramref name="regraRemanejamento"/> e <paramref name="acaoQuandoIndeferido"/>
    /// são opcionais.
    /// </summary>
    /// <remarks>
    /// A factory <b>aceita</b> os treze códigos do catálogo legal fixo — recusá-los é
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
        PredicadoDnf? regraDeAplicabilidade,
        string? acaoQuandoIndeferido,
        string? baseLegal)
    {
        List<FieldError> erros = [];

        Result<CodigoModalidade> codigoResult = CodigoModalidade.Criar(codigo);
        if (codigoResult.IsFailure)
        {
            erros.Add(new("codigo", codigoResult.Error!));
        }

        Result<CamposResolvidosDaModalidade> camposResult = ValidacaoDosCamposDaModalidade.ValidarCampos(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            regraDeAplicabilidade, acaoQuandoIndeferido, baseLegal,
            codigoResult.IsSuccess ? codigoResult.Value : null);
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
        PredicadoDnf? regraDeAplicabilidade,
        string? acaoQuandoIndeferido,
        string? baseLegal)
    {
        CamposValidadosDaModalidade validados = ValidacaoDosCamposDaModalidade.ValidarCamposIndependentes(new DeclaracaoDaModalidade(
            descricao, naturezaLegal, composicaoVagas, composicaoOrigem, regraRemanejamento,
            remanejamentoDestino, remanejamentoPar, remanejamentoFallback,
            regraDeAplicabilidade, acaoQuandoIndeferido, baseLegal));
        if (validados.Recusas.Count > 0)
        {
            return Result.ValidationFailure(validados.Recusas);
        }

        CamposResolvidosDaModalidade campos = validados.Campos;

        if (Codigo.EhLegalFixa && EstruturaDivergeDe(campos))
        {
            return Result.Failure(new DomainError(
                ModalidadeErrorCodes.EstruturaProtegidaNaoEditavel,
                "Esta modalidade pertence ao catálogo legal fixo e admite alteração apenas de "
                + "descrição e base legal — natureza, composição, remanejamento, regra de aplicabilidade "
                + "e ação no indeferimento são fixados em lei."));
        }

        List<FieldError> violacoes = CoerenciaDosCamposDaModalidade.ValidarCoerenciaCruzada(campos, validados.Reconhecidos);
        violacoes.AddRange(CoerenciaDosCamposDaModalidade.ValidarAutorreferencia(Codigo, campos));
        if (violacoes.Count > 0)
        {
            return Result.ValidationFailure(violacoes);
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
    private bool EstruturaDivergeDe(CamposResolvidosDaModalidade campos) =>
        campos.NaturezaLegal != NaturezaLegal
        || campos.ComposicaoVagas != ComposicaoVagas
        || !string.Equals(campos.ComposicaoOrigem, ComposicaoOrigem, StringComparison.Ordinal)
        || campos.RegraRemanejamento != RegraRemanejamento
        || campos.RemanejamentoArgs != RemanejamentoArgs
        || !campos.RegraDeAplicabilidade!.MesmoConteudo(RegraDeAplicabilidade)
        || campos.AcaoQuandoIndeferido != AcaoQuandoIndeferido;

    private void AplicarCampos(CamposResolvidosDaModalidade campos)
    {
        Descricao = campos.Descricao;
        NaturezaLegal = campos.NaturezaLegal;
        ComposicaoVagas = campos.ComposicaoVagas;
        ComposicaoOrigem = campos.ComposicaoOrigem;
        RegraRemanejamento = campos.RegraRemanejamento;
        RemanejamentoArgs = campos.RemanejamentoArgs;
        RegraDeAplicabilidade = campos.RegraDeAplicabilidade!;
        AcaoQuandoIndeferido = campos.AcaoQuandoIndeferido;
        BaseLegal = campos.BaseLegal;
    }

}
