namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public static class FiltroDaListagemDeMotivos
{
    public const string TipoInvalido = "MotivoDeDecisao.FiltroDeTipoInvalido";
    public const string SubclassificacaoInvalida = "MotivoDeDecisao.FiltroDeSubclassificacaoInvalida";

    public static Result<FiltroDosMotivos> Ler(string? tipo, string? subclassificacao, bool apenasAtivos)
    {
        Result<TipoMotivoDeDecisao?> tipoLido = FiltroPorCodigoPedido.Ler<TipoMotivoDeDecisao>(tipo,
            new DomainError(TipoInvalido, "Tipo inválido. Use um dos códigos publicados para o tipo de motivo."));
        Result<FundamentoIsencao?> fundamento = FiltroPorCodigoPedido.Ler<FundamentoIsencao>(subclassificacao,
            new DomainError(SubclassificacaoInvalida, "Subclassificação inválida. Use um dos códigos publicados para o fundamento de isenção."));
        List<FieldError> recusas = Recusas(tipoLido, fundamento);
        return recusas.Count > 0
            ? Result<FiltroDosMotivos>.ValidationFailure(recusas)
            : Result<FiltroDosMotivos>.Success(new FiltroDosMotivos(tipoLido.Value, fundamento.Value?.Codigo(), apenasAtivos));
    }

    private static List<FieldError> Recusas(Result<TipoMotivoDeDecisao?> tipo, Result<FundamentoIsencao?> fundamento)
    {
        List<FieldError> recusas = [];
        if (tipo.IsFailure)
        {
            recusas.Add(new FieldError("tipo", tipo.Error!));
        }

        if (fundamento.IsFailure)
        {
            recusas.Add(new FieldError("subclassificacao", fundamento.Error!));
        }

        return recusas;
    }
}
