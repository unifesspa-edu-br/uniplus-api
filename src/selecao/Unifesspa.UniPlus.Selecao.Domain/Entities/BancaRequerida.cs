namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca requerida por uma <see cref="FaseCronograma"/> (0..*, Story #851 §4) —
/// snapshot-copy (ADR-0061) de um <c>TipoBanca</c> do módulo Configuração no momento em
/// que foi vinculada, mais os <b>candidatos atendidos</b>, que dizem do que ela responde.
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
/// é obrigatório e quando duas bancas atendem os mesmos candidatos é invariante da FASE, que enxerga
/// todas as bancas de uma vez:
/// <see cref="ProcessosSeletivos.Cronograma.CandidatosAtendidosPelasBancas.Violacoes"/>.
/// </para>
/// </remarks>
public sealed class BancaRequerida : EntityBase
{
    public Guid FaseCronogramaId { get; private set; }

    /// <summary>Id (Guid v7) do <c>TipoBanca</c> vivo de origem, no momento do congelamento.</summary>
    public Guid TipoBancaOrigemId { get; private set; }

    /// <summary>Código classificatório congelado (ex.: <c>"BANCA_ANALISE_DOCUMENTAL"</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    /// <summary>Nome do tipo de banca, copiado do cadastro no congelamento (ADR-0061).</summary>
    public string Nome { get; private set; } = string.Empty;

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
        Guid tipoBancaOrigemId,
        string codigo,
        string nome,
        IReadOnlyList<CategoriaJulgada> candidatosAtendidos,
        RegraDeDeliberacao? regraDeDeliberacao = null)
    {
        (string codigoAparado, string nomeAparado) = OrigemDaBanca.Exigir(tipoBancaOrigemId, codigo, nome);
        ArgumentNullException.ThrowIfNull(candidatosAtendidos);

        BancaRequerida banca = new()
        {
            TipoBancaOrigemId = tipoBancaOrigemId,
            Codigo = codigoAparado,
            Nome = nomeAparado,
            RegraDeDeliberacao = regraDeDeliberacao,
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
}
