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
