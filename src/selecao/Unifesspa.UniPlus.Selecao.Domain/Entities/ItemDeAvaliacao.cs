namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um item de avaliação da <see cref="EtapaProcesso"/> (critério ou bloco): componente da nota
/// da etapa, com a pontuação máxima que vale.
/// </summary>
/// <remarks>
/// O lançamento e o recurso apontam para o item, então o <see cref="EntityBase.Id"/> dele é
/// congelado no envelope e preservado na retificação, mesmo quando o rótulo muda.
/// </remarks>
public sealed class ItemDeAvaliacao : EntityBase
{
    /// <summary>Alinhado a <c>ItemDeAvaliacaoConfiguration</c> (varchar(300)).</summary>
    public const int RotuloMaxLength = 300;

    public const string RotuloInvalido = "ItemDeAvaliacao.RotuloInvalido";
    public const string PontuacaoMaximaInvalida = "ItemDeAvaliacao.PontuacaoMaximaInvalida";
    public const string PontuacaoForaDoPasso = "ItemDeAvaliacao.PontuacaoForaDoPasso";
    public const string RotuloDuplicado = "ItemDeAvaliacao.RotuloDuplicado";
    public const string IdDuplicado = "ItemDeAvaliacao.IdDuplicado";

    public ChaveLocal Chave { get; private set; }

    public Guid EtapaProcessoId { get; private set; }

    public string Rotulo { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    /// <summary>O quanto o item vale da nota máxima da etapa.</summary>
    public decimal PontuacaoMaxima { get; private set; }

    private ItemDeAvaliacao() { }

    internal static ItemDeAvaliacao Criar(ChaveLocal chave, ItemDeAvaliacaoDeclarado declarado)
    {
        ArgumentNullException.ThrowIfNull(declarado);

        return new ItemDeAvaliacao { Chave = chave, Rotulo = declarado.Rotulo.Trim(), PontuacaoMaxima = declarado.PontuacaoMaxima };
    }

    /// <summary>Reidrata o item preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static ItemDeAvaliacao Reidratar(ChaveLocal chave, Guid id, string rotulo, decimal pontuacaoMaxima)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rotulo);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O item reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new ItemDeAvaliacao { Chave = chave, Id = id, Rotulo = rotulo.Trim(), PontuacaoMaxima = pontuacaoMaxima };
    }

    /// <summary>Troca rótulo e pontuação preservando a linha: o id é a identidade do critério.</summary>
    internal void Atualizar(string rotulo, decimal pontuacaoMaxima)
    {
        Rotulo = rotulo.Trim();
        PontuacaoMaxima = pontuacaoMaxima;
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;

    internal void DefinirOrdem(int ordem) => Ordem = ordem;
}
