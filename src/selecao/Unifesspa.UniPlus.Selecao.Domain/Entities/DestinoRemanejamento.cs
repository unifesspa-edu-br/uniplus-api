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
    /// <summary>Alinhado a <c>DestinoRemanejamentoConfiguration</c> (varchar(60)).</summary>
    public const int CodigoMaxLength = 60;

    [GeneratedRegex("^[A-Z0-9_]+$")]
    private static partial Regex CodigoValido();

    public Guid ConfiguracaoCascataRemanejamentoId { get; private set; }
    public string ModalidadeOrigemCodigo { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public string ModalidadeDestinoCodigo { get; private set; } = string.Empty;

    private DestinoRemanejamento() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — o
    /// chamador (o handler, iterando <c>destinos[i]</c>) funde estes erros com o índice do
    /// item na lista antes de devolver o lote completo (ADR-0023: <c>errors[]</c> não
    /// reflete o dado rejeitado, por isso as mensagens abaixo não ecoam os códigos/ordem
    /// recebidos).
    /// </summary>
    public static Result<DestinoRemanejamento> Criar(string? modalidadeOrigemCodigo, int ordem, string? modalidadeDestinoCodigo)
    {
        List<FieldError> erros = [];

        bool origemValida = CodigoEhValido(modalidadeOrigemCodigo);
        if (!origemValida)
        {
            erros.Add(new("modalidadeOrigemCodigo", new DomainError(
                "ConfiguracaoCascataRemanejamento.CodigoInvalido",
                $"Código de origem inválido — precisa casar com ^[A-Z0-9_]+$ e ter no máximo {CodigoMaxLength} caracteres.")));
        }

        bool destinoValido = CodigoEhValido(modalidadeDestinoCodigo);
        if (!destinoValido)
        {
            erros.Add(new("modalidadeDestinoCodigo", new DomainError(
                "ConfiguracaoCascataRemanejamento.CodigoInvalido",
                $"Código de destino inválido — precisa casar com ^[A-Z0-9_]+$ e ter no máximo {CodigoMaxLength} caracteres.")));
        }

        if (ordem < 1)
        {
            erros.Add(new("ordem", new DomainError(
                "ConfiguracaoCascataRemanejamento.OrdemInvalida",
                "A ordem do destino deve ser ≥ 1.")));
        }

        if (origemValida && destinoValido && string.Equals(modalidadeOrigemCodigo, modalidadeDestinoCodigo, StringComparison.Ordinal))
        {
            erros.Add(new("modalidadeDestinoCodigo", new DomainError(
                "ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino",
                "Uma modalidade não pode remanejar para si mesma.")));
        }

        if (erros.Count > 0)
        {
            return Result<DestinoRemanejamento>.ValidationFailure(erros);
        }

        return Result<DestinoRemanejamento>.Success(new DestinoRemanejamento
        {
            ModalidadeOrigemCodigo = modalidadeOrigemCodigo!,
            Ordem = ordem,
            ModalidadeDestinoCodigo = modalidadeDestinoCodigo!,
        });
    }

    private static bool CodigoEhValido(string? codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && codigo.Length <= CodigoMaxLength && CodigoValido().IsMatch(codigo);

    internal void VincularCascata(Guid configuracaoCascataRemanejamentoId) =>
        ConfiguracaoCascataRemanejamentoId = configuracaoCascataRemanejamentoId;
}
