namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
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

    public Guid EtapaProcessoId { get; private set; }

    public string Rotulo { get; private set; } = string.Empty;

    /// <summary>O quanto o item vale da nota máxima da etapa.</summary>
    public decimal PontuacaoMaxima { get; private set; }

    private ItemDeAvaliacao() { }

    /// <summary>
    /// Acumula toda violação dos itens declarados (ADR-0125). A soma das pontuações contra a nota
    /// máxima da etapa não é conferida aqui: depende do grafo, e em rascunho a divergência é
    /// tolerada, então é item do gate de publicação.
    /// </summary>
    public static List<FieldError> Validar(IReadOnlyList<ItemDeAvaliacaoDeclarado?>? itens, decimal? passoDoLancamento)
    {
        List<FieldError> erros = [];
        if (itens is null)
        {
            return erros;
        }

        HashSet<string> rotulos = new(StringComparer.Ordinal);
        HashSet<Guid> ids = [];
        for (int i = 0; i < itens.Count; i++)
        {
            if (itens[i] is not { } item)
            {
                continue;
            }

            string campo = $"itensDeAvaliacao[{i}]";
            string rotulo = item.Rotulo?.Trim() ?? string.Empty;
            if (rotulo.Length is 0 or > RotuloMaxLength)
            {
                erros.Add(new($"{campo}.rotulo", new DomainError(
                    RotuloInvalido,
                    $"O rótulo do item de avaliação é obrigatório e deve ter no máximo {RotuloMaxLength} caracteres.")));
            }
            else if (!rotulos.Add(HashCanonicalComputer.NormalizeNfc(rotulo)))
            {
                erros.Add(new($"{campo}.rotulo", new DomainError(
                    RotuloDuplicado,
                    $"O item '{rotulo}' aparece mais de uma vez na etapa: o rótulo identifica o critério para a banca.")));
            }

            if (item.Id is { } id && !ids.Add(id))
            {
                erros.Add(new($"{campo}.id", new DomainError(
                    IdDuplicado,
                    "O mesmo id de item de avaliação não pode ser informado mais de uma vez na etapa.")));
            }

            if (item.PontuacaoMaxima <= 0)
            {
                erros.Add(new($"{campo}.pontuacaoMaxima", new DomainError(
                    PontuacaoMaximaInvalida,
                    "A pontuação máxima do item de avaliação deve ser maior que zero.")));
            }
            else if (passoDoLancamento > 0 && item.PontuacaoMaxima % passoDoLancamento != 0)
            {
                erros.Add(new($"{campo}.pontuacaoMaxima", new DomainError(
                    PontuacaoForaDoPasso,
                    "A pontuação máxima do item deve ser múltipla do passo do lançamento da etapa.")));
            }
        }

        return erros;
    }

    internal static ItemDeAvaliacao Criar(ItemDeAvaliacaoDeclarado declarado)
    {
        ArgumentNullException.ThrowIfNull(declarado);

        return new ItemDeAvaliacao { Rotulo = declarado.Rotulo.Trim(), PontuacaoMaxima = declarado.PontuacaoMaxima };
    }

    /// <summary>Reidrata o item preservando o <see cref="EntityBase.Id"/> congelado.</summary>
    public static ItemDeAvaliacao Reidratar(Guid id, string rotulo, decimal pontuacaoMaxima)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rotulo);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O item reidratado deve declarar o Id congelado no envelope.", nameof(id));
        }

        return new ItemDeAvaliacao { Id = id, Rotulo = rotulo.Trim(), PontuacaoMaxima = pontuacaoMaxima };
    }

    /// <summary>Troca rótulo e pontuação preservando a linha: o id é a identidade do critério.</summary>
    internal void Atualizar(string rotulo, decimal pontuacaoMaxima)
    {
        Rotulo = rotulo.Trim();
        PontuacaoMaxima = pontuacaoMaxima;
    }

    internal void VincularEtapa(Guid etapaProcessoId) => EtapaProcessoId = etapaProcessoId;
}
