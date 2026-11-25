namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Lê o tipo solicitado para a consulta.</summary>
public static class FiltroDeTipoDasRegrasDoCatalogo
{
    public const string CodigoDeErro = "RegraCatalogo.FiltroDeTipoInvalido";

    public static Result<TipoRegra?> Ler(string? tipo) =>
        FiltroPorCodigoPedido.Ler<TipoRegra>(tipo, new DomainError(CodigoDeErro, "Tipo inválido. Use um dos códigos publicados para o tipo de regra."));
}
