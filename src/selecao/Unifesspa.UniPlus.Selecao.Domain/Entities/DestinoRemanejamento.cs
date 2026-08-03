namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma posição na fila legal de remanejamento de uma modalidade de origem
/// (Story #575, RN-CASCATA-4): quando a vaga de <see cref="ModalidadeOrigemCodigo"/>
/// não é preenchida, ela é oferecida a <see cref="ModalidadeDestinoCodigo"/>, na
/// posição <see cref="Ordem"/> da fila daquela origem.
/// </summary>
/// <remarks>
/// Entidade filha de <see cref="ConfiguracaoCascataRemanejamento"/>, substituída
/// por inteiro a cada redefinição da cascata (sem soft-delete). A ampla
/// concorrência nunca aparece aqui — é o <c>FallbackCodigo</c> da cascata, campo
/// separado (§2.3 da story).
/// </remarks>
public sealed partial class DestinoRemanejamento : EntityBase
{
    private const int CodigoMaxLength = 60;

    [GeneratedRegex("^[A-Z0-9_]+$")]
    private static partial Regex CodigoValido();

    public Guid ConfiguracaoCascataRemanejamentoId { get; private set; }
    public string ModalidadeOrigemCodigo { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public string ModalidadeDestinoCodigo { get; private set; } = string.Empty;

    private DestinoRemanejamento() { }

    public static Result<DestinoRemanejamento> Criar(string modalidadeOrigemCodigo, int ordem, string modalidadeDestinoCodigo)
    {
        if (!CodigoEhValido(modalidadeOrigemCodigo))
        {
            return Result<DestinoRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.CodigoInvalido",
                $"Código de origem \"{modalidadeOrigemCodigo}\" inválido — precisa casar com ^[A-Z0-9_]+$ e ter no máximo {CodigoMaxLength} caracteres."));
        }

        if (!CodigoEhValido(modalidadeDestinoCodigo))
        {
            return Result<DestinoRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.CodigoInvalido",
                $"Código de destino \"{modalidadeDestinoCodigo}\" inválido — precisa casar com ^[A-Z0-9_]+$ e ter no máximo {CodigoMaxLength} caracteres."));
        }

        if (ordem < 1)
        {
            return Result<DestinoRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.OrdemInvalida",
                $"A ordem do destino deve ser ≥ 1 — recebida {ordem}."));
        }

        if (string.Equals(modalidadeOrigemCodigo, modalidadeDestinoCodigo, StringComparison.Ordinal))
        {
            return Result<DestinoRemanejamento>.Failure(new DomainError(
                "ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino",
                $"A modalidade \"{modalidadeOrigemCodigo}\" não pode remanejar para si mesma."));
        }

        return Result<DestinoRemanejamento>.Success(new DestinoRemanejamento
        {
            ModalidadeOrigemCodigo = modalidadeOrigemCodigo,
            Ordem = ordem,
            ModalidadeDestinoCodigo = modalidadeDestinoCodigo,
        });
    }

    private static bool CodigoEhValido(string codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && codigo.Length <= CodigoMaxLength && CodigoValido().IsMatch(codigo);

    internal void VincularCascata(Guid configuracaoCascataRemanejamentoId) =>
        ConfiguracaoCascataRemanejamentoId = configuracaoCascataRemanejamentoId;
}
