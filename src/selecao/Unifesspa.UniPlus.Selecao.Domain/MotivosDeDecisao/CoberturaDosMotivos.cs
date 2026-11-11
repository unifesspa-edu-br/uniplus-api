namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Portão da publicação, da retificação e do fechamento da sessão: o processo só congela com os
/// motivos que as suas decisões exigem. Cada decisão que passa a exigir motivo acrescenta aqui a
/// sua exigência; hoje vale a da isenção da taxa (UNI-REQ-0123): ao menos um motivo favorável e um
/// desfavorável escolhidos para cada fundamento de isenção habilitado.
/// </summary>
public static class CoberturaDosMotivos
{
    private static readonly SentidoDoMotivo[] Sentidos = [SentidoDoMotivo.Favoravel, SentidoDoMotivo.Desfavoravel];

    /// <summary>Sucesso quando a cobertura está completa; senão, uma recusa por fundamento e sentido que falta.</summary>
    public static Result Conferir(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhidos);

        FieldError[] faltas = [.. FaltasDaIsencao(processo, escolhidos)];
        return faltas.Length > 0 ? Result.ValidationFailure(faltas) : Result.Success();
    }

    private static IEnumerable<FieldError> FaltasDaIsencao(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        IReadOnlyList<FundamentoIsencao> fundamentos = processo.ConfiguracaoTaxaInscricao is { Cobra: true } taxa ? taxa.Fundamentos : [];

        return from fundamento in fundamentos
               from sentido in Sentidos
               where !escolhidos.Any(e => CobreAIsencao(e, fundamento.ToCodigo(), sentido))
               select new FieldError("motivosDeDecisao", new DomainError(
                   MotivoDeDecisaoErrorCodes.CoberturaInsuficiente,
                   $"Falta escolher ao menos um motivo {sentido.Rotulo()} para o fundamento de isenção {fundamento.ToCodigo()}."));
    }

    private static bool CobreAIsencao(MotivoDoProcesso escolhido, string fundamento, SentidoDoMotivo sentido) =>
        escolhido.Tipo == TipoMotivoDeDecisao.IsencaoDaTaxa
        && escolhido.Sentido == sentido
        && string.Equals(escolhido.Subclassificacao, fundamento, StringComparison.Ordinal);
}
