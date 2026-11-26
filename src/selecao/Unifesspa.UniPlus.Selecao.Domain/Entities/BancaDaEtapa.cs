namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca que a <see cref="EtapaProcesso"/> requer (0..*) — cópia do <c>TipoBanca</c> do
/// cadastro (<see cref="Tipo"/>), no mesmo desenho de <see cref="BancaRequerida"/> um nível acima.
/// </summary>
/// <remarks>
/// Existe porque quem julga é declarado por etapa, não pela fase inteira: na habilitação, a
/// banca de heteroidentificação julga a etapa de cota racial e a banca biopsicossocial
/// julga a de laudo — duas bancas distintas dentro da mesma fase, que um vínculo no nível
/// da fase não separa.
/// </remarks>
public sealed class BancaDaEtapa : EntityBase, IBancaDoLugar<BancaDaEtapa>
{
    public ChaveLocal Chave { get; private set; }

    public Guid EtapaProcessoId { get; private set; }

    /// <summary>
    /// A posição da banca entre as do mesmo tipo na etapa, a partir de 1, na ordem em que a etapa as recebeu. Junto do código
    /// do tipo, é única na etapa e é o que ordena as bancas.
    /// </summary>
    public int PosicaoNoTipo { get; private set; }

    /// <summary>O id da banca já gravada que esta declaração substitui; ausente quando a banca é nova.</summary>
    private Guid? IdDaBancaQueSubstitui { get; init; }

    /// <summary>
    /// O tipo de banca copiado do cadastro: o código, o papel, as marcas e os tipos de decisão que ele julga. Cada banca é
    /// julgada pela própria cópia, mantida enquanto o tipo dela não muda.
    /// </summary>
    public TipoDeBancaCopiado Tipo { get; private set; } = null!;

    /// <summary>Como a banca é composta e delibera; ausente enquanto o rascunho não a declara.</summary>
    public RegraDeDeliberacao? RegraDeDeliberacao { get; private set; }

    /// <summary>Consequência própria da ausência do candidato, quando declarada.</summary>
    public ConsequenciaDoDescumprimento? ConsequenciaDaAusencia { get; private set; }

    private BancaDaEtapa() { }

    public static BancaDaEtapa Criar(
        ChaveLocal chave,
        TipoDeBancaCopiado tipo,
        RegraDeDeliberacao? regraDeDeliberacao = null,
        Guid? idDaBancaQueSubstitui = null,
        ConsequenciaDoDescumprimento? consequenciaDaAusencia = null) =>
        Montar(chave, Guid.CreateVersion7(), tipo, regraDeDeliberacao, idDaBancaQueSubstitui, consequenciaDaAusencia);

    /// <summary>Reconstrói a banca dos dados salvos no envelope, preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static BancaDaEtapa ReconstruirDosDadosSalvos(ChaveLocal chave, Guid id, TipoDeBancaCopiado tipo, RegraDeDeliberacao? regraDeDeliberacao = null, ConsequenciaDoDescumprimento? consequenciaDaAusencia = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A banca reconstruída deve declarar o Id congelado no envelope.", nameof(id));
        }

        return Montar(chave, id, tipo, regraDeDeliberacao, id, consequenciaDaAusencia);
    }

    private static BancaDaEtapa Montar(ChaveLocal chave, Guid id, TipoDeBancaCopiado tipo, RegraDeDeliberacao? regraDeDeliberacao, Guid? idDaBancaQueSubstitui, ConsequenciaDoDescumprimento? consequenciaDaAusencia)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return new BancaDaEtapa
        {
            Id = id,
            Chave = chave,
            Tipo = tipo,
            RegraDeDeliberacao = regraDeDeliberacao,
            ConsequenciaDaAusencia = consequenciaDaAusencia,
            IdDaBancaQueSubstitui = idDaBancaQueSubstitui,
        };
    }

    /// <summary>
    /// Assume a declaração que substitui esta banca, preservando o <see cref="EntityBase.Id"/>: a banca que continua
    /// declarada continua sendo a mesma linha, a cópia do tipo só é trocada quando o tipo muda, e é a posição no tipo que a ordena.
    /// </summary>
    void IBancaDoLugar<BancaDaEtapa>.AssumirDeclaracao(BancaDaEtapa declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        Tipo = Tipo.OrigemId == declarada.Tipo.OrigemId ? Tipo : declarada.Tipo;
        RegraDeDeliberacao = declarada.RegraDeDeliberacao;
        ConsequenciaDaAusencia = declarada.ConsequenciaDaAusencia;
        PosicaoNoTipo = declarada.PosicaoNoTipo;
    }

    void IBancaDoLugar<BancaDaEtapa>.PosicionarNoTipo(int posicaoNoTipo) => PosicaoNoTipo = posicaoNoTipo;

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;

    Guid? IBancaDoLugar<BancaDaEtapa>.IdDaBancaQueSubstitui => IdDaBancaQueSubstitui;
}
