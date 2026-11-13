namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca requerida por uma <see cref="FaseCronograma"/> (0..*, Story #851 §4) —
/// snapshot-copy (ADR-0061) de um <c>TipoBanca</c> do módulo Configuração no momento em
/// que foi vinculada, mais o <b>recorte de competência</b> que diz do que ela responde.
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
/// <see cref="RecorteDeCompetencia"/> as duas são a mesma linha repetida. Quando o recorte
/// é obrigatório e quando dois recortes se confundem é invariante da FASE, que enxerga
/// todas as bancas de uma vez:
/// <see cref="FaseCronograma.ViolacoesDoRecorteDeCompetencia(string, System.Collections.Generic.IReadOnlyList{BancaRequerida})"/>.
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

    private readonly List<CategoriaJulgada> _recorteDeCompetencia = [];

    /// <summary>
    /// As categorias de documento que esta banca julga (0..*) — vazio quando o tipo já
    /// identifica a banca sozinho dentro da fase.
    /// </summary>
    public IReadOnlyCollection<CategoriaJulgada> RecorteDeCompetencia => _recorteDeCompetencia.AsReadOnly();

    private BancaRequerida() { }

    public static BancaRequerida Criar(
        Guid tipoBancaOrigemId,
        string codigo,
        string nome,
        IReadOnlyList<CategoriaJulgada> recorteDeCompetencia,
        RegraDeDeliberacao? regraDeDeliberacao = null)
    {
        (string codigoAparado, string nomeAparado) = OrigemDaBanca.Exigir(tipoBancaOrigemId, codigo, nome);
        ArgumentNullException.ThrowIfNull(recorteDeCompetencia);

        BancaRequerida banca = new()
        {
            TipoBancaOrigemId = tipoBancaOrigemId,
            Codigo = codigoAparado,
            Nome = nomeAparado,
            RegraDeDeliberacao = regraDeDeliberacao,
        };
        banca.AdotarRecorte(recorteDeCompetencia);
        return banca;
    }

    private void AdotarRecorte(IReadOnlyList<CategoriaJulgada> recorteDeCompetencia)
    {
        foreach (CategoriaJulgada categoria in recorteDeCompetencia)
        {
            categoria.VincularBanca(Id);
            _recorteDeCompetencia.Add(categoria);
        }
    }

    internal void VincularFase(Guid faseCronogramaId) => FaseCronogramaId = faseCronogramaId;

    /// <summary>
    /// Os códigos do recorte, sem repetição e em ordem canônica — a forma pela qual dois
    /// recortes são comparados.
    /// </summary>
    /// <remarks>
    /// A comparação é por <b>código</b>, não pelo id de origem: o código é a chave natural
    /// da categoria no cadastro e é o que o operador lê no edital publicado. Ordenar e
    /// deduplicar aqui é o que faz "mesmo conjunto declarado em outra ordem" contar como o
    /// mesmo recorte — que é o caso que a distinção entre bancas do mesmo tipo precisa
    /// recusar.
    /// </remarks>
    internal IReadOnlyList<string> ChaveDoRecorte() =>
        [.. _recorteDeCompetencia
            .Select(static c => c.Codigo)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
}
