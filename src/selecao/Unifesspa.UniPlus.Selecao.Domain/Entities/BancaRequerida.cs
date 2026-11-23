namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca requerida por uma <see cref="FaseCronograma"/> (0..*, Story #851 §4) —
/// cópia (ADR-0061) do <c>TipoBanca</c> do módulo Configuração no momento em que foi vinculada
/// (<see cref="Tipo"/>), mais os <b>candidatos atendidos</b>, que dizem do que ela responde.
/// </summary>
/// <remarks>
/// <para>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por inteiro
/// (<see cref="ProcessoSeletivo.DefinirCronogramaFases"/>).
/// </para>
/// <para>
/// <b>O tipo sozinho nem sempre identifica a banca.</b> Uma mesma fase de análise
/// documental pode requerer duas bancas do mesmo tipo — uma que analisa critério de renda,
/// outra que analisa requisito étnico-racial para fins de cota —, e sem
/// <see cref="CandidatosAtendidos"/> as duas são a mesma linha repetida. Quando declará-los
/// é obrigatório é invariante da FASE, que enxerga todas as bancas de uma vez:
/// <see cref="ProcessosSeletivos.Cronograma.CandidatosAtendidosPelasBancas.Violacoes"/>.
/// </para>
/// </remarks>
public sealed class BancaRequerida : EntityBase, IBancaDoLugar<BancaRequerida>
{
    public ChaveLocal Chave { get; private set; }

    public Guid FaseCronogramaId { get; private set; }

    /// <summary>
    /// A posição da banca entre as do mesmo tipo na fase, a partir de 1, na ordem em que a fase as recebeu. Junto do código
    /// do tipo, é única na fase e é o que ordena as bancas.
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

    private readonly List<CategoriaJulgada> _candidatosAtendidos = [];

    /// <summary>
    /// As categorias de documento que esta banca julga (0..*) — vazio quando o tipo já
    /// identifica a banca sozinho dentro da fase.
    /// </summary>
    public IReadOnlyCollection<CategoriaJulgada> CandidatosAtendidos => _candidatosAtendidos.AsReadOnly();

    private BancaRequerida() { }

    public static BancaRequerida Criar(
        ChaveLocal chave,
        TipoDeBancaCopiado tipo,
        IReadOnlyList<CategoriaJulgada> candidatosAtendidos,
        RegraDeDeliberacao? regraDeDeliberacao = null,
        Guid? idDaBancaQueSubstitui = null) =>
        Montar(chave, Guid.CreateVersion7(), tipo, candidatosAtendidos, regraDeDeliberacao, idDaBancaQueSubstitui);

    /// <summary>Reconstrói a banca dos dados salvos no envelope, preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static BancaRequerida ReconstruirDosDadosSalvos(
        ChaveLocal chave,
        Guid id,
        TipoDeBancaCopiado tipo,
        IReadOnlyList<CategoriaJulgada> candidatosAtendidos,
        RegraDeDeliberacao? regraDeDeliberacao = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A banca reconstruída deve declarar o Id congelado no envelope.", nameof(id));
        }

        return Montar(chave, id, tipo, candidatosAtendidos, regraDeDeliberacao, id);
    }

    private static BancaRequerida Montar(
        ChaveLocal chave,
        Guid id,
        TipoDeBancaCopiado tipo,
        IReadOnlyList<CategoriaJulgada> candidatosAtendidos,
        RegraDeDeliberacao? regraDeDeliberacao,
        Guid? idDaBancaQueSubstitui)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(candidatosAtendidos);

        BancaRequerida banca = new()
        {
            Id = id,
            Chave = chave,
            Tipo = tipo,
            RegraDeDeliberacao = regraDeDeliberacao,
            IdDaBancaQueSubstitui = idDaBancaQueSubstitui,
        };
        banca.AdotarCandidatosAtendidos(candidatosAtendidos);
        return banca;
    }

    private void AdotarCandidatosAtendidos(IReadOnlyList<CategoriaJulgada> candidatosAtendidos)
    {
        foreach (CategoriaJulgada categoria in candidatosAtendidos)
        {
            categoria.VincularBanca(Id);
            _candidatosAtendidos.Add(categoria);
        }
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;

    void IBancaDoLugar<BancaRequerida>.PosicionarNoTipo(int posicaoNoTipo) => PosicaoNoTipo = posicaoNoTipo;

    /// <summary>
    /// Assume a declaração que substitui esta banca, preservando o <see cref="EntityBase.Id"/>. A cópia do tipo só é trocada
    /// quando o tipo muda. A categoria de mesmo código continua sendo a mesma linha, porque o par banca e código é único e
    /// sair e voltar sob ele colidiria dentro da mesma transação.
    /// </summary>
    void IBancaDoLugar<BancaRequerida>.AssumirDeclaracao(BancaRequerida declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        Tipo = Tipo.OrigemId == declarada.Tipo.OrigemId ? Tipo : declarada.Tipo;
        RegraDeDeliberacao = declarada.RegraDeDeliberacao;
        PosicaoNoTipo = declarada.PosicaoNoTipo;
        ReconciliarCandidatosAtendidos(declarada._candidatosAtendidos);
    }

    private void ReconciliarCandidatosAtendidos(IReadOnlyList<CategoriaJulgada> declaradas)
    {
        List<CategoriaJulgada> vivas = [.. _candidatosAtendidos];
        List<CategoriaJulgada> mantidas = [.. declaradas.Select(declarada => ReaproveitarOuAdotar(vivas, declarada))];

        _candidatosAtendidos.Clear();
        AdotarCandidatosAtendidos(mantidas);
    }

    private static CategoriaJulgada ReaproveitarOuAdotar(List<CategoriaJulgada> vivas, CategoriaJulgada declarada)
    {
        CategoriaJulgada? viva = vivas.Find(candidata => string.Equals(candidata.Codigo, declarada.Codigo, StringComparison.Ordinal));
        if (viva is null)
        {
            return declarada;
        }

        vivas.Remove(viva);
        viva.AssumirOrigem(declarada);

        return viva;
    }

    /// <summary>
    /// Os códigos dos candidatos atendidos, sem repetição e em ordem canônica — a forma pela
    /// qual dois conjuntos de candidatos atendidos são comparados.
    /// </summary>
    /// <remarks>
    /// A comparação é por <b>código</b>, não pelo id de origem: o código é a chave natural
    /// da categoria no cadastro e é o que o operador lê no edital publicado. Ordenar e
    /// deduplicar aqui é o que faz "mesmo conjunto declarado em outra ordem" contar como o
    /// mesmo conjunto — que é o caso que a distinção entre bancas do mesmo tipo precisa
    /// recusar.
    /// </remarks>
    internal IReadOnlyList<string> ChaveDosCandidatosAtendidos() =>
        [.. _candidatosAtendidos
            .Select(static c => c.Codigo)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    Guid? IBancaDoLugar<BancaRequerida>.IdDaBancaQueSubstitui => IdDaBancaQueSubstitui;
}
