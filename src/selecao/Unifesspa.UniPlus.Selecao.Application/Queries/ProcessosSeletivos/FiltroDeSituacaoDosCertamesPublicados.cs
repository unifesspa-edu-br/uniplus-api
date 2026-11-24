namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Lê a situação da consulta sem repetir valores desconhecidos na recusa.</summary>
public static class FiltroDeSituacaoDosCertamesPublicados
{
    public const string CodigoDeErro = "Certame.FiltroDeSituacaoInvalido";

    public static Result<SituacaoDoCertame?> Ler(string? situacao)
    {
        if (string.IsNullOrWhiteSpace(situacao))
        {
            return Result<SituacaoDoCertame?>.Success(null);
        }

        return CatalogoDe<SituacaoDoCertame>.TentarObter(situacao, out SituacaoDoCertame valor)
            ? Result<SituacaoDoCertame?>.Success(valor)
            : Result<SituacaoDoCertame?>.Failure(new DomainError(
                CodigoDeErro,
                "Situação inválida. Use um dos códigos publicados para a situação do certame."));
    }
}
