namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Etapa <em>pontuada</em> do <see cref="ProcessoSeletivo"/> (peso, caráter e
/// nota mínima) — distinta de fase do cronograma, que é o eixo temporal do
/// certame. Entidade interna do agregado: criada, substituída e persistida
/// exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete) de propósito:
/// a configuração em rascunho é substituível por inteiro (comandos
/// <c>Definir*</c>) e a trilha auditável do que valeu em cada publicação é o
/// snapshot RN08 da Story #759 — não faz sentido acumular linhas logicamente
/// excluídas de rascunho. A <c>Etapa</c> ligada a <c>Edital</c> continua
/// existindo para a conformidade da Story #460 e é aposentada junto com o
/// CRUD de Edital na #759.
/// </remarks>
public sealed class EtapaProcesso : EntityBase
{
    public Guid ProcessoSeletivoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public CaraterEtapa Carater { get; private set; }
    public decimal? Peso { get; private set; }
    public decimal? NotaMinima { get; private set; }
    public int? Ordem { get; private set; }

    private EtapaProcesso() { }

    public static EtapaProcesso Criar(
        string nome,
        CaraterEtapa carater,
        decimal? peso = null,
        decimal? notaMinima = null,
        int? ordem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).",
                nameof(carater));
        }

        return new EtapaProcesso
        {
            Nome = nome.Trim(),
            Carater = carater,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
        };
    }

    /// <summary>
    /// Reidrata uma etapa a partir de uma <see cref="VersaoConfiguracao"/> congelada,
    /// <b>preservando o <see cref="EntityBase.Id"/></b> — que <see cref="Criar"/> não
    /// aceita, por decisão (a identidade de uma etapa nova é do sistema, não do cliente).
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <c>id</c> da etapa é o <b>único</b> id de entidade-filha que o envelope congela
    /// (ADR-0110 D2), porque <c>criteriosDesempate.args.etapaRef</c> e
    /// <c>regrasEliminacao.args.etapaRef</c> apontam para ele: sem preservá-lo, o
    /// snapshot reidratado teria referências que não resolvem, e o desempate e a
    /// eliminação do certame ficariam inexecutáveis. Os ids das demais filhas são
    /// regenerados — nenhuma referência de negócio exige estabilidade deles, e as FKs
    /// internas são reconstruídas junto com o grafo.
    /// </para>
    /// <para>
    /// Reidratar não é criar: os dados vêm de um documento com peso jurídico, já
    /// validado quando foi congelado. As guardas aqui são a última linha contra erro de
    /// programação — o decoder do envelope é quem recusa bytes inválidos, e o faz com
    /// <c>Result</c>, nunca com exceção.
    /// </para>
    /// </remarks>
    public static EtapaProcesso Reidratar(
        Guid id,
        string nome,
        CaraterEtapa carater,
        decimal? peso,
        decimal? notaMinima,
        int? ordem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A etapa reidratada deve declarar o Id congelado no envelope.", nameof(id));
        }

        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).",
                nameof(carater));
        }

        return new EtapaProcesso
        {
            Id = id,
            Nome = nome.Trim(),
            Carater = carater,
            Peso = peso,
            NotaMinima = notaMinima,
            Ordem = ordem,
        };
    }

    /// <summary>
    /// Uma etapa compõe a nota final quando o seu caráter pontua
    /// (classificatória ou ambas) e ela declara peso — é o critério que a
    /// inclui no divisor da média (<see cref="ProcessoSeletivo.CalcularDivisorMedia"/>).
    /// </summary>
    public bool ComponeNota => Carater is CaraterEtapa.Classificatoria or CaraterEtapa.Ambas && Peso.HasValue;

    /// <summary>
    /// Atualiza os dados da MESMA etapa (mesmo <see cref="EntityBase.Id"/>) em
    /// vez de recriá-la — permite que <c>DefinirEtapasCommandHandler</c>
    /// reconcilie o payload de <c>PUT /etapas</c> com o agregado tracked
    /// preservando a identidade de uma etapa já referenciada por critério de
    /// desempate ou regra de eliminação da classificação (sem isso, qualquer
    /// reconfiguração de etapas quebraria essas referências por construção).
    /// </summary>
    public void AtualizarDados(
        string nome,
        CaraterEtapa carater,
        decimal? peso,
        decimal? notaMinima,
        int? ordem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (carater == CaraterEtapa.Nenhum)
        {
            throw new ArgumentException(
                "Caráter da etapa é obrigatório (classificatória, eliminatória ou ambas).",
                nameof(carater));
        }

        Nome = nome.Trim();
        Carater = carater;
        Peso = peso;
        NotaMinima = notaMinima;
        Ordem = ordem;
    }

    internal void VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;
}
