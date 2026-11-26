namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Resolve os filtros da lista pública na ordem de recusa.</summary>
public static class FiltroPedidoDosCertamesPublicados
{
    public static Result<FiltroDosCertamesPublicados> Resolver(string? situacao, string? modalidade, string? busca)
    {
        Result<SituacaoDoCertame?> situacaoLida = FiltroDeSituacaoDosCertamesPublicados.Ler(situacao);
        if (situacaoLida.IsFailure)
        {
            return Result<FiltroDosCertamesPublicados>.Failure(situacaoLida.Error!);
        }

        Result<TermoDeBusca?> termo = TermoDeBusca.Criar(busca);
        return termo.IsSuccess
            ? Result<FiltroDosCertamesPublicados>.Success(new(situacaoLida.Value, modalidade, termo.Value))
            : Result<FiltroDosCertamesPublicados>.Failure(termo.Error!);
    }
}
