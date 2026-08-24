namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do detalhe. Devolve o mesmo hash que o domínio resolve ao
/// congelar a referência — é o que permite a quem retoma um rascunho conferir que a definição
/// lida é exatamente a que a configuração aponta.
/// </summary>
public static class ObterRegraCatalogoQueryHandler
{
    public static async Task<Result<RegraCatalogoDto>> Handle(
        ObterRegraCatalogoQuery query,
        IRegraCatalogoReader reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reader);

        RegraCatalogo? regra = await reader
            .ObterAsync(query.Codigo, query.Versao, cancellationToken)
            .ConfigureAwait(false);

        // Código inexistente e versão inexistente de um código conhecido são a mesma resposta:
        // a identidade da regra é o par, e não o código sozinho. Distinguir os dois revelaria
        // quais códigos existem no catálogo a quem só tentou adivinhar.
        return regra is null
            ? Result<RegraCatalogoDto>.Failure(new DomainError(
                "RegraCatalogo.NaoEncontrada",
                $"Não há a versão '{query.Versao}' da regra '{query.Codigo}' no catálogo."))
            : Result<RegraCatalogoDto>.Success(RegraCatalogoMapping.ToDto(regra));
    }
}
