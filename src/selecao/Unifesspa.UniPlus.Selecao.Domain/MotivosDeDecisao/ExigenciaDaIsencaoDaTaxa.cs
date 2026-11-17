namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Cobertura dos motivos da decisão sobre a isenção da taxa (UNI-REQ-0123): ao menos um motivo
/// favorável e um desfavorável escolhidos para cada fundamento de isenção habilitado.
/// </summary>
public sealed class ExigenciaDaIsencaoDaTaxa : IExigenciaDeMotivos
{
    private static readonly SentidoDoMotivo[] Sentidos = [SentidoDoMotivo.Favoravel, SentidoDoMotivo.Desfavoravel];

    public IEnumerable<FieldError> Faltas(ProcessoSeletivo processo, IReadOnlyCollection<MotivoDoProcesso> escolhidos)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(escolhidos);

        IReadOnlyList<FundamentoIsencao> fundamentos = processo.ConfiguracaoTaxaInscricao is { Cobra: true } taxa ? taxa.Fundamentos : [];

        return from fundamento in fundamentos
               from sentido in Sentidos
               where !escolhidos.Any(e => Cobre(e, fundamento.ToCodigo(), sentido))
               select new FieldError("motivos", new DomainError(
                   MotivoDeDecisaoErrorCodes.CoberturaInsuficiente,
                   $"Falta escolher ao menos um motivo {sentido.Rotulo()} para o fundamento de isenção {fundamento.ToCodigo()}."));
    }

    private static bool Cobre(MotivoDoProcesso escolhido, string fundamento, SentidoDoMotivo sentido) =>
        escolhido.Tipo == TipoMotivoDeDecisao.IsencaoDaTaxa
        && escolhido.Sentido == sentido
        && string.Equals(escolhido.Subclassificacao, fundamento, StringComparison.Ordinal);
}
