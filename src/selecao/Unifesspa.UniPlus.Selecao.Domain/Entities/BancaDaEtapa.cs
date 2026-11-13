namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca que a <see cref="EtapaProcesso"/> requer (0..*) — snapshot-copy do
/// <c>TipoBanca</c> do cadastro, no mesmo desenho de <see cref="BancaRequerida"/> um nível
/// acima.
/// </summary>
/// <remarks>
/// Existe porque quem julga é declarado por etapa, não pela fase inteira: na habilitação, a
/// banca de heteroidentificação julga a etapa de cota racial e a banca biopsicossocial
/// julga a de laudo — duas bancas distintas dentro da mesma fase, que um vínculo no nível
/// da fase não separa.
/// </remarks>
public sealed class BancaDaEtapa : EntityBase
{
    public Guid EtapaProcessoId { get; private set; }

    /// <summary>Id do <c>TipoBanca</c> vivo de origem, no momento do congelamento.</summary>
    public Guid TipoBancaOrigemId { get; private set; }

    /// <summary>Código classificatório congelado (ex.: <c>"BANCA_HETEROIDENTIFICACAO"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Nome do tipo de banca, copiado do cadastro no congelamento (ADR-0061).</summary>
    public string Nome { get; private set; } = string.Empty;

    /// <summary>Como a banca é composta e delibera; ausente enquanto o rascunho não a declara.</summary>
    public RegraDeDeliberacao? RegraDeDeliberacao { get; private set; }

    private BancaDaEtapa() { }

    public static BancaDaEtapa Criar(
        Guid tipoBancaOrigemId, string codigo, string nome, RegraDeDeliberacao? regraDeDeliberacao = null) =>
        Montar(Guid.CreateVersion7(), tipoBancaOrigemId, codigo, nome, regraDeDeliberacao);

    /// <summary>Reidrata a banca preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static BancaDaEtapa Reidratar(
        Guid id, Guid tipoBancaOrigemId, string codigo, string nome, RegraDeDeliberacao? regraDeDeliberacao = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A banca reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        return Montar(id, tipoBancaOrigemId, codigo, nome, regraDeDeliberacao);
    }

    private static BancaDaEtapa Montar(
        Guid id, Guid tipoBancaOrigemId, string codigo, string nome, RegraDeDeliberacao? regraDeDeliberacao)
    {
        (string codigoAparado, string nomeAparado) = OrigemDaBanca.Exigir(tipoBancaOrigemId, codigo, nome);

        return new BancaDaEtapa
        {
            Id = id,
            TipoBancaOrigemId = tipoBancaOrigemId,
            Codigo = codigoAparado,
            Nome = nomeAparado,
            RegraDeDeliberacao = regraDeDeliberacao,
        };
    }

    /// <summary>
    /// Alinha nome e regra desta linha viva aos da declaração que a substitui: a etapa reaproveita a
    /// linha de mesmo código, e o que não é a origem do tipo precisa acompanhá-la.
    /// </summary>
    internal void Redeclarar(BancaDaEtapa declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        Nome = declarada.Nome;
        RegraDeDeliberacao = declarada.RegraDeDeliberacao;
    }

    /// <summary>
    /// Repõe o tipo de origem preservando a linha — o código é a chave do índice único da
    /// etapa, e sair e voltar sob o mesmo código colidiria dentro da própria transação.
    /// </summary>
    internal void ReporOrigem(Guid tipoBancaOrigemId) => TipoBancaOrigemId = tipoBancaOrigemId;

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
