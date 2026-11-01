namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O circuito de aprovação da proteção de dados de um item do catálogo — tipo de documento ou fato
/// (ADR-0153): o item nasce pendente, o administrador o envia numa solicitação, a proteção de dados
/// o aprova ou o nega com a classificação que recomenda, e o administrador aceita a recomendação ou
/// edita o item. Guarda quem agiu e quando em cada passo, só o ciclo corrente: mudar a classificação, a
/// hipótese legal ou a finalidade do item reinicia o circuito.
/// </summary>
/// <remarks>
/// Mutável por dentro do dono, que o mapeia na própria tabela: a transição é sempre um método
/// do dono, que sabe qual proteção mudou e qual a classificação que a recomendação aceita vira.
/// </remarks>
public sealed class AprovacaoDaProtecao
{
    public SituacaoDaAprovacao Situacao { get; private set; } = SituacaoDaAprovacao.Pendente;

    public DateTimeOffset? SolicitadaEm { get; private set; }

    public string? SolicitadaPor { get; private set; }

    public DateTimeOffset? DecididaEm { get; private set; }

    public string? DecididaPor { get; private set; }

    /// <summary>A classificação que a proteção de dados indicou ao negar; guardada também depois do aceite.</summary>
    public ClassificacaoDeProtecao? ClassificacaoRecomendada { get; private set; }

    public DateTimeOffset? AceitaEm { get; private set; }

    public string? AceitaPor { get; private set; }

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
        if (Situacao != SituacaoDaAprovacao.Solicitada)
        {
            return Result.Failure(ItemForaDeSolicitacao("Só o item que está numa solicitação é aprovado."));
        }

        Situacao = SituacaoDaAprovacao.Aprovada;
        Decidir(ator, em);
        return Result.Success();
    }

    internal Result Negar(ClassificacaoDeProtecao recomendada, string ator, DateTimeOffset em)
    {
        if (Situacao != SituacaoDaAprovacao.Solicitada)
        {
            return Result.Failure(ItemForaDeSolicitacao("Só o item que está numa solicitação é negado."));
        }

        Situacao = SituacaoDaAprovacao.Negada;
        ClassificacaoRecomendada = recomendada;
        Decidir(ator, em);
        return Result.Success();
    }

    /// <summary>A classificação recomendada pela negativa, ou a falha quando o item não está negado.</summary>
    internal Result<ClassificacaoDeProtecao> RecomendacaoAAceitar() =>
        Situacao == SituacaoDaAprovacao.Negada && ClassificacaoRecomendada is { } recomendada
            ? Result<ClassificacaoDeProtecao>.Success(recomendada)
            : Result<ClassificacaoDeProtecao>.Failure(SemRecomendacao);

    private static readonly DomainError SemRecomendacao = new(
        ProtecaoDoCatalogoErrorCodes.SemRecomendacaoParaAceitar,
        "Só o item negado pela proteção de dados tem recomendação a aceitar.");

    /// <summary>
    /// Aprova o item pela recomendação já lida por <see cref="RecomendacaoAAceitar"/>: só a partir de NEGADA, e só
    /// com a classificação que a negativa indicou.
    /// </summary>
    internal Result AceitarRecomendacao(ClassificacaoDeProtecao aplicada, string ator, DateTimeOffset em)
    {
        if (Situacao != SituacaoDaAprovacao.Negada || ClassificacaoRecomendada != aplicada)
        {
            return Result.Failure(SemRecomendacao);
        }

        Situacao = SituacaoDaAprovacao.Aprovada;
        AceitaEm = em;
        AceitaPor = ator;
        return Result.Success();
    }

    /// <summary>A proteção do item mudou: o circuito recomeça e o registro do ciclo anterior sai.</summary>
    internal void Reiniciar()
    {
        Situacao = SituacaoDaAprovacao.Pendente;
        SolicitadaEm = null;
        SolicitadaPor = null;
        DecididaEm = null;
        DecididaPor = null;
        ClassificacaoRecomendada = null;
        AceitaEm = null;
        AceitaPor = null;
    }

    /// <summary>
    /// A classificação recomendada pela negativa é obrigatória, pertence à escala, difere da que o item tem e cabe na
    /// hipótese legal dele: a recomendação que o aceite não poderia aplicar é recusada na própria negativa, para o
    /// aceite não ficar sem saída.
    /// </summary>
    internal static Result ConferirRecomendacao(
        ClassificacaoDeProtecao recomendada, ClassificacaoDeProtecao atual, HipoteseLegalTratamento hipoteseLegal)
    {
        if (recomendada == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(recomendada))
        {
            return Recusa(
                ProtecaoDoCatalogoErrorCodes.RecomendacaoObrigatoria,
                "A negativa exige a classificação de proteção de dados recomendada, dentro do vocabulário fechado.");
        }

        if (recomendada == atual)
        {
            return Recusa(
                ProtecaoDoCatalogoErrorCodes.RecomendacaoIgualAAtual,
                "A classificação recomendada é a que o item já tem: aprove o item, ou recomende a classificação correta.");
        }

        return HipotesesLegaisTratamento.AdmiteClassificacao(hipoteseLegal, recomendada)
            ? Result.Success()
            : Recusa(
                ProtecaoDoCatalogoErrorCodes.RecomendacaoIncompativelComAHipoteseLegal,
                "A hipótese legal do item não cabe na classificação recomendada: o administrador não poderia aceitá-la. "
                + "Recomende uma classificação que a hipótese admita.");
    }

    private static Result Recusa(string codigo, string mensagem) =>
        Result.ValidationFailure([new FieldError("classificacaoRecomendada", new DomainError(codigo, mensagem))]);

    private void Decidir(string ator, DateTimeOffset em)
    {
        DecididaEm = em;
        DecididaPor = ator;
    }

    private static DomainError ItemForaDeSolicitacao(string mensagem) =>
        new(ProtecaoDoCatalogoErrorCodes.ItemForaDeSolicitacao, mensagem);
}
