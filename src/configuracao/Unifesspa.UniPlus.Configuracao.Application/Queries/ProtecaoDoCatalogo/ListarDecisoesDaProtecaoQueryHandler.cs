namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Lê o registro das decisões da proteção de dados: a encarregada vê todas e restringe às próprias com
/// <c>minhas</c>, que compara o emissor e o <c>sub</c> do token. Os critérios são conferidos antes de qualquer leitura (ADR-0125).
/// </summary>
public static class ListarDecisoesDaProtecaoQueryHandler
{
    public static async Task<Result<ListarDecisoesDaProtecaoResult>> Handle(
        ListarDecisoesDaProtecaoQuery query,
        IAtoDaProtecaoDoCatalogoRepository atos,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(userContext);

        List<FieldError> erros = [];

        List<AtoDaProtecao> decisoes = [];
        foreach (string token in query.Atos)
        {
            if (AtosDaProtecao.TryAnalisar(token, out AtoDaProtecao ato) && AtosDaProtecao.Decisoes.Contains(ato))
            {
                decisoes.Add(ato);
            }
            else
            {
                erros.Add(new("ato", new DomainError(
                    ProtecaoDoCatalogoErrorCodes.AtoInvalido,
                    "A decisão deve ser APROVACAO, AJUSTE_RECOMENDADO ou REPROVACAO.")));
            }
        }

        CadastroDoCatalogo? cadastro = null;
        if (query.Cadastro is not null)
        {
            if (CadastrosDoCatalogo.TryAnalisar(query.Cadastro, out CadastroDoCatalogo analisado))
            {
                cadastro = analisado;
            }
            else
            {
                erros.Add(new("cadastro", new DomainError(
                    ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
                    $"O cadastro do item deve ser {CadastrosDaProtecaoDoCatalogo.TipoDocumento} ou {CadastrosDaProtecaoDoCatalogo.Fato}.")));
            }
        }

        if (query.De is { } de && query.Ate is { } ate && de > ate)
        {
            erros.Add(new("de", new DomainError(
                ProtecaoDoCatalogoErrorCodes.PeriodoInvalido, "A data inicial não pode ser posterior à data final.")));
        }

        Result<string?> busca = BuscaPedida.Validar(query.Busca);
        if (busca.IsFailure)
        {
            erros.Add(new("busca", busca.Error!));
        }

        if (erros.Count > 0)
        {
            return Result<ListarDecisoesDaProtecaoResult>.ValidationFailure(erros);
        }

        AtorDoAto quemConsulta = AtorDoToken.De(userContext);
        FiltroDasDecisoesDaProtecao filtro = new(
            decisoes,
            cadastro,
            query.De,
            query.Ate,
            busca.Value,
            query.Minhas ? (quemConsulta.Emissor, quemConsulta.Sub) : null);

        (IReadOnlyList<AtoDaProtecaoDoCatalogo> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await atos
                .ListarDecisoesAsync(filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);

        DecisaoDaProtecaoDto[] items = [.. itens.Select(static a => a.ToDecisaoDto())];
        return Result<ListarDecisoesDaProtecaoResult>.Success(new ListarDecisoesDaProtecaoResult(items, anterior, proximo));
    }
}
