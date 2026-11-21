namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
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
public sealed class BancaDaEtapa : EntityBase, IBancaDoLugar<BancaDaEtapa>
{
    public Guid EtapaProcessoId { get; private set; }

    /// <summary>
    /// A posição da banca entre as do mesmo tipo na etapa, a partir de 1, na ordem em que a etapa as recebeu. Junto do código
    /// do tipo, é única na etapa e é o que ordena as bancas.
    /// </summary>
    public int PosicaoNoTipo { get; private set; }

    /// <summary>O id da banca já gravada que esta declaração substitui; ausente quando a banca é nova.</summary>
    private Guid? IdDaBancaQueSubstitui { get; init; }

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
        Guid tipoBancaOrigemId,
        string codigo,
        string nome,
        RegraDeDeliberacao? regraDeDeliberacao = null,
        Guid? idDaBancaQueSubstitui = null) =>
        Montar(Guid.CreateVersion7(), tipoBancaOrigemId, codigo, nome, regraDeDeliberacao, idDaBancaQueSubstitui);

    /// <summary>Reconstrói a banca dos dados salvos no envelope, preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static BancaDaEtapa ReconstruirDosDadosSalvos(
        Guid id, Guid tipoBancaOrigemId, string codigo, string nome, RegraDeDeliberacao? regraDeDeliberacao = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A banca reconstruída deve declarar o Id congelado no envelope.", nameof(id));
        }

        return Montar(id, tipoBancaOrigemId, codigo, nome, regraDeDeliberacao, id);
    }

    private static BancaDaEtapa Montar(
        Guid id,
        Guid tipoBancaOrigemId,
        string codigo,
        string nome,
        RegraDeDeliberacao? regraDeDeliberacao,
        Guid? idDaBancaQueSubstitui)
    {
        (string codigoAparado, string nomeAparado) = OrigemDaBanca.Exigir(tipoBancaOrigemId, codigo, nome);

        return new BancaDaEtapa
        {
            Id = id,
            TipoBancaOrigemId = tipoBancaOrigemId,
            Codigo = codigoAparado,
            Nome = nomeAparado,
            RegraDeDeliberacao = regraDeDeliberacao,
            IdDaBancaQueSubstitui = idDaBancaQueSubstitui,
        };
    }

    /// <summary>
    /// Assume a declaração que substitui esta banca, preservando o <see cref="EntityBase.Id"/>: a banca que continua
    /// declarada continua sendo a mesma linha, e é a posição no tipo que a ordena.
    /// </summary>
    void IBancaDoLugar<BancaDaEtapa>.AssumirDeclaracao(BancaDaEtapa declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        TipoBancaOrigemId = declarada.TipoBancaOrigemId;
        Codigo = declarada.Codigo;
        Nome = declarada.Nome;
        RegraDeDeliberacao = declarada.RegraDeDeliberacao;
        PosicaoNoTipo = declarada.PosicaoNoTipo;
    }

    void IBancaDoLugar<BancaDaEtapa>.PosicionarNoTipo(int posicaoNoTipo) => PosicaoNoTipo = posicaoNoTipo;

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;

    Guid? IBancaDoLugar<BancaDaEtapa>.IdDaBancaQueSubstitui => IdDaBancaQueSubstitui;
}
