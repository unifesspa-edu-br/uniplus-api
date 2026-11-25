namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// Lê o filtro da listagem de motivos. O tipo chega como texto e é convertido pela mesma regra
/// que a criação usa, e não por binding no enum: ligado ao enum, o parâmetro aceitaria a grafia
/// dos membros em C# e recusaria justamente o código que a API devolve em <c>tipo</c>.
/// </summary>
internal static class FiltroDaListagemDeMotivos
{
    public static Result<FiltroDosMotivos> Ler(string? tipo, string? subclassificacao, bool apenasAtivos)
    {
        string? subclassificacaoFiltrada = string.IsNullOrWhiteSpace(subclassificacao) ? null : subclassificacao;
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return Result<FiltroDosMotivos>.Success(new FiltroDosMotivos(null, subclassificacaoFiltrada, apenasAtivos));
        }

        Result<TipoMotivoDeDecisao> tipoFiltrado = TipoDoMotivoInformado.Converter(tipo);
        return tipoFiltrado.IsFailure
            ? Result<FiltroDosMotivos>.ValidationFailure(tipoFiltrado.Errors)
            : Result<FiltroDosMotivos>.Success(new FiltroDosMotivos(tipoFiltrado.Value, subclassificacaoFiltrada, apenasAtivos));
    }
}
