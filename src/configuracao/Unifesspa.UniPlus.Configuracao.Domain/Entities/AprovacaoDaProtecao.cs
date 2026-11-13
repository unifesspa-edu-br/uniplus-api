namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O circuito de aprovação da proteção de dados de um item do catálogo — tipo de documento ou fato
/// (ADR-0153): o item nasce pendente, o administrador o envia numa solicitação, a proteção de dados
/// o aprova, recomenda o ajuste da classificação ou o reprova com a justificativa, e o administrador aceita a
/// recomendação do ajuste ou edita o item. Guarda quem agiu e quando em cada passo, só o ciclo corrente: mudar a classificação, a
/// hipótese legal ou a finalidade do item reinicia o circuito, e editar o item com ajuste recomendado ou reprovado
/// também o reinicia, seja qual for o campo. O reinício de item já solicitado abre um novo ciclo, que encerra as
/// manifestações técnicas do anterior (ADR-0163).
/// </summary>
/// <remarks>
/// Mutável por dentro do dono, que o mapeia na própria tabela: a transição é sempre um método
/// do dono, que sabe qual proteção mudou e qual a classificação que a recomendação aceita vira.
/// </remarks>
public sealed class AprovacaoDaProtecao
{
    private static readonly FieldError RecomendacaoAusente = RecusaDaRecomendacao(
        ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria,
        "O ajuste exige a classificação de proteção de dados recomendada, dentro do vocabulário fechado.");

    private static readonly FieldError RecomendacaoIgualAAtual = RecusaDaRecomendacao(
        ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual,
        "A classificação recomendada é a que o item já tem: aprove o item, ou recomende a classificação correta.");

    private static readonly FieldError RecomendacaoForaDaHipotese = RecusaDaRecomendacao(
        ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal,
        "A hipótese legal do item não cabe na classificação recomendada: o administrador não poderia aceitá-la. "
        + "Recomende uma classificação que a hipótese admita.");

    private static readonly DomainError AceiteDeItemReprovado = new(
        ProtecaoDoCatalogoErrorCodes.AceiteDeItemReprovado,
        "O item reprovado não tem recomendação a aceitar: edite o item e peça a aprovação de novo.");

    private static readonly DomainError SemRecomendacao = new(
        ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar,
        "Só o item com ajuste recomendado pela proteção de dados tem recomendação a aceitar.");

    public SituacaoDaAprovacao Situacao { get; private set; } = SituacaoDaAprovacao.Pendente;

    /// <summary>O ciclo de aprovação corrente; avança a cada reinício de item que já entrou numa solicitação.</summary>
    public CicloDaAprovacao Ciclo { get; private set; } = CicloDaAprovacao.Primeiro;

    public DateTimeOffset? SolicitadaEm { get; private set; }

    public string? SolicitadaPor { get; private set; }

    public DateTimeOffset? DecididaEm { get; private set; }

    public string? DecididaPor { get; private set; }

    /// <summary>A classificação que a proteção de dados indicou ao recomendar o ajuste; guardada também depois do aceite.</summary>
    public ClassificacaoDeProtecao? ClassificacaoRecomendada { get; private set; }

    /// <summary>
    /// O motivo do ajuste recomendado (opcional) ou da reprovação (obrigatório). A justificativa da aprovação fica só no
    /// ato da decisão.
    /// </summary>
    public string? Justificativa { get; private set; }

    public DateTimeOffset? AceitaEm { get; private set; }

    public string? AceitaPor { get; private set; }

    /// <summary>O instante da última manifestação técnica do ciclo; nulo enquanto não houver manifestação.</summary>
    public DateTimeOffset? ManifestadaEm { get; private set; }

    /// <summary>
    /// Se há, no ciclo, manifestação técnica vigente com parecer recomenda ajuste ou inadequada: diante dela, aprovar
    /// ou recomendar ajuste exige a justificativa (UNI-REQ-0374). Fica na linha do item para a manifestação e a decisão
    /// disputarem o mesmo controle de concorrência (ADR-0163).
    /// </summary>
    public bool ManifestacaoDesfavoravelVigente { get; private set; }

    /// <summary>
    /// Quantos eventos de manifestação técnica (registros e substituições) o ciclo recebeu. Cada manifestação o avança,
    /// então toda manifestação muda a linha do item e disputa o token de concorrência da decisão, mesmo quando o
    /// instante e o indicador desfavorável não mudam (relógio repetido, nova manifestação adequada) (ADR-0163, ADR-0119).
    /// </summary>
    public int ManifestacoesNoCiclo { get; private set; }

    /// <summary>Só o item aprovado pode ser usado na configuração e na publicação de processo seletivo.</summary>
    public bool EstaAprovada => Situacao == SituacaoDaAprovacao.Aprovada;

    /// <summary>Passa o item pendente à solicitação enviada por <paramref name="ator"/>.</summary>
    internal Result Solicitar(string ator, DateTimeOffset em)
    {
        if (Situacao != SituacaoDaAprovacao.Pendente)
        {
            return Result.Failure(ItemForaDeSolicitacao("Só o item pendente entra numa solicitação."));
        }

        Situacao = SituacaoDaAprovacao.Solicitada;
        SolicitadaEm = em;
        SolicitadaPor = ator;
        return Result.Success();
    }

    internal Result Aprovar(string ator, DateTimeOffset em)
    {
        Result<Fundamentacao?> conferida = ConferirDecisao(null, obrigatoria: false, "Só o item que está numa solicitação é aprovado.");
        if (conferida.IsFailure)
        {
            return Result.ValidationFailure(conferida.Errors);
        }

        Situacao = SituacaoDaAprovacao.Aprovada;
        Decidir(ator, em);
        return Result.Success();
    }

    internal Result RecomendarAjuste(ClassificacaoDeProtecao recomendada, string? justificativa, string ator, DateTimeOffset em)
    {
        Result<Fundamentacao?> conferida = ConferirDecisao(
            justificativa, obrigatoria: false, "Só o item que está numa solicitação recebe a recomendação de ajuste.");
        if (conferida.IsFailure)
        {
            return Result.ValidationFailure(conferida.Errors);
        }

        Situacao = SituacaoDaAprovacao.AjusteRecomendado;
        ClassificacaoRecomendada = recomendada;
        Justificativa = conferida.Value?.Valor;
        Decidir(ator, em);
        return Result.Success();
    }

    internal Result Reprovar(string? justificativa, string ator, DateTimeOffset em)
    {
        Result<Fundamentacao?> conferida = ConferirDecisao(justificativa, obrigatoria: true, "Só o item que está numa solicitação é reprovado.");
        if (conferida.IsFailure)
        {
            return Result.ValidationFailure(conferida.Errors);
        }

        Situacao = SituacaoDaAprovacao.Reprovada;
        Justificativa = conferida.Value?.Valor;
        Decidir(ator, em);
        return Result.Success();
    }

    /// <summary>
    /// A classificação recomendada pelo ajuste, ou a falha quando o item não tem ajuste recomendado: o item
    /// reprovado não tem recomendação, e o aceite é recusado com código próprio.
    /// </summary>
    internal Result<ClassificacaoDeProtecao> RecomendacaoAAceitar()
    {
        if (Situacao == SituacaoDaAprovacao.Reprovada)
        {
            return Result<ClassificacaoDeProtecao>.Failure(AceiteDeItemReprovado);
        }

        return Situacao == SituacaoDaAprovacao.AjusteRecomendado && ClassificacaoRecomendada is { } recomendada
            ? Result<ClassificacaoDeProtecao>.Success(recomendada)
            : Result<ClassificacaoDeProtecao>.Failure(SemRecomendacao);
    }

    /// <summary>
    /// Aprova o item pela recomendação já lida por <see cref="RecomendacaoAAceitar"/>: só a partir de
    /// AJUSTE_RECOMENDADO, e só com a classificação que o ajuste indicou. O aceite não reinicia o ciclo nem exige
    /// justificativa, mesmo diante de manifestação desfavorável (UNI-REQ-0374).
    /// </summary>
    internal Result AceitarRecomendacao(ClassificacaoDeProtecao aplicada, string ator, DateTimeOffset em)
    {
        if (Situacao != SituacaoDaAprovacao.AjusteRecomendado || ClassificacaoRecomendada != aplicada)
        {
            return Result.Failure(SemRecomendacao);
        }

        Situacao = SituacaoDaAprovacao.Aprovada;
        AceitaEm = em;
        AceitaPor = ator;
        return Result.Success();
    }

    /// <summary>
    /// A manifestação técnica registrada no ciclo: mais um evento contado, o instante dele e se, depois dele, há
    /// manifestação desfavorável vigente. Só <see cref="ManifestacoesDoCiclo"/> chama, depois de conferir o ciclo e a situação; a mudança na linha
    /// do item é o que faz a manifestação disputar o controle de concorrência da decisão (ADR-0163).
    /// </summary>
    internal void RegistrarManifestacao(bool desfavoravelVigente, DateTimeOffset em)
    {
        ManifestacoesNoCiclo++;
        ManifestadaEm = em;
        ManifestacaoDesfavoravelVigente = desfavoravelVigente;
    }

    /// <summary>
    /// O item foi editado e salvo. A mudança da proteção reinicia o circuito de qualquer situação; o item com ajuste
    /// recomendado ou reprovado também recomeça por qualquer edição, porque a decisão pode ter sido sobre qualquer
    /// campo. O aprovado só recomeça se a proteção mudou; a edição que não reinicia mantém o ciclo e as manifestações.
    /// </summary>
    internal void AoEditarItem(bool protecaoMudou)
    {
        if (protecaoMudou || Situacao is SituacaoDaAprovacao.AjusteRecomendado or SituacaoDaAprovacao.Reprovada)
        {
            Reiniciar();
        }
    }

    /// <summary>
    /// A classificação recomendada pelo ajuste é obrigatória, pertence à escala, difere da que o item tem e cabe na
    /// hipótese legal dele: a recomendação que o aceite não poderia aplicar é recusada no próprio ajuste, para o
    /// aceite não ficar sem saída.
    /// </summary>
    internal static Result ConferirRecomendacao(
        ClassificacaoDeProtecao recomendada, ClassificacaoDeProtecao atual, HipoteseLegalTratamento hipoteseLegal)
    {
        if (recomendada == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(recomendada))
        {
            return Result.ValidationFailure([RecomendacaoAusente]);
        }

        if (recomendada == atual)
        {
            return Result.ValidationFailure([RecomendacaoIgualAAtual]);
        }

        return HipotesesLegaisTratamento.AdmiteClassificacao(hipoteseLegal, recomendada)
            ? Result.Success()
            : Result.ValidationFailure([RecomendacaoForaDaHipotese]);
    }

    // O circuito recomeça e o registro do ciclo anterior sai dele; os atos e as manifestações continuam nos registros
    // próprios. Só o item que já entrou numa solicitação abre um ciclo novo.
    private void Reiniciar()
    {
        Ciclo = Situacao == SituacaoDaAprovacao.Pendente ? Ciclo : Ciclo.Seguinte();
        Situacao = SituacaoDaAprovacao.Pendente;
        SolicitadaEm = null;
        SolicitadaPor = null;
        DecididaEm = null;
        DecididaPor = null;
        ClassificacaoRecomendada = null;
        Justificativa = null;
        AceitaEm = null;
        AceitaPor = null;
        ManifestadaEm = null;
        ManifestacaoDesfavoravelVigente = false;
        ManifestacoesNoCiclo = 0;
    }

    // A justificativa é conferida antes da situação, como o pedido a confere antes de ler o item (ADR-0125).
    private Result<Fundamentacao?> ConferirDecisao(string? justificativa, bool obrigatoria, string foraDeSolicitacao)
    {
        List<FieldError> erros = [];
        Fundamentacao? conferida = Fundamentacao.Ler(justificativa, obrigatoria, RecusasDaFundamentacao.DaJustificativa, erros);
        if (erros.Count > 0)
        {
            return Result<Fundamentacao?>.ValidationFailure(erros);
        }

        return Situacao == SituacaoDaAprovacao.Solicitada
            ? Result<Fundamentacao?>.Success(conferida)
            : Result<Fundamentacao?>.Failure(ItemForaDeSolicitacao(foraDeSolicitacao));
    }

    private void Decidir(string ator, DateTimeOffset em)
    {
        DecididaEm = em;
        DecididaPor = ator;
    }

    private static FieldError RecusaDaRecomendacao(string codigo, string mensagem) =>
        new("classificacaoRecomendada", new DomainError(codigo, mensagem));

    private static DomainError ItemForaDeSolicitacao(string mensagem) =>
        new(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao, mensagem);
}
