namespace Unifesspa.UniPlus.Selecao.Application.Queries.NotasDoEnem;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Leiautes;

/// <summary>Lê o tipo solicitado para a consulta.</summary>
public static class FiltroDeTipoDosLeiautesDoInep
{
    public const string CodigoDeErro = "LeiauteDoInep.FiltroDeTipoInvalido";

    public static Result<TipoDeLeiauteDoInep?> Ler(string? tipo) =>
        FiltroPorCodigoPedido.Ler<TipoDeLeiauteDoInep>(tipo, new DomainError(CodigoDeErro, "Tipo inválido. Use um dos códigos publicados para o tipo de leiaute do INEP."));
}
