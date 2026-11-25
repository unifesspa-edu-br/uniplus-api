namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma linha do quadro de vagas — a quantidade ofertada de uma modalidade
/// dentro de uma <see cref="ConfiguracaoDistribuicaoVagas"/> (ADR-0115).
/// Output derivado: nasce dentro de
/// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/> (calculado no ramo
/// federal, fixado no institucional), nunca por comando próprio.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="ModalidadeSelecionada"/>: o quadro em rascunho é substituível
/// por inteiro junto com a configuração que o produziu.
/// </remarks>
public sealed class VagaOfertada : EntityBase
{
    public Guid ConfiguracaoDistribuicaoVagasId { get; private set; }
    public Guid ModalidadeOrigemId { get; private set; }
    public string ModalidadeCodigo { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }

    private VagaOfertada() { }

    /// <summary>
    /// Cria a linha do quadro. A quantidade vem do cálculo do quadro, depois de a conferência da forma recusar
    /// a quantidade declarada negativa; uma negativa aqui é defeito de programa.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A quantidade é negativa.</exception>
    public static Result<VagaOfertada> Criar(Guid modalidadeOrigemId, string modalidadeCodigo, int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantidade);

        if (string.IsNullOrWhiteSpace(modalidadeCodigo))
        {
            return Result<VagaOfertada>.Failure(new DomainError(
                "VagaOfertada.ModalidadeCodigoObrigatorio", "Código da modalidade é obrigatório."));
        }

        return Result<VagaOfertada>.Success(new VagaOfertada
        {
            ModalidadeOrigemId = modalidadeOrigemId,
            ModalidadeCodigo = modalidadeCodigo,
            Quantidade = quantidade,
        });
    }

    internal void VincularConfiguracao(Guid configuracaoDistribuicaoVagasId) =>
        ConfiguracaoDistribuicaoVagasId = configuracaoDistribuicaoVagasId;
}
