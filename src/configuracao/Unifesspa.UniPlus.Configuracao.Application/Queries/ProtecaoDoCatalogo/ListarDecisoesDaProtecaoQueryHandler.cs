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
/// Lê o registro das decisões da proteção do catálogo: o Representante do controlador vê todas e restringe às próprias com
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
        FiltroDasDecisoesDaProtecao filtro = Filtro(query, AtorDoToken.De(userContext), erros);
        return erros.Count > 0
            ? Result<ListarDecisoesDaProtecaoResult>.ValidationFailure(erros)
            : await ListarAsync(query, filtro, atos, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<ListarDecisoesDaProtecaoResult>> ListarAsync(
        ListarDecisoesDaProtecaoQuery query, FiltroDasDecisoesDaProtecao filtro, IAtoDaProtecaoDoCatalogoRepository atos, CancellationToken cancellationToken)
    {
        (IReadOnlyList<AtoDaProtecaoDoCatalogo> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) =
            await atos
                .ListarDecisoesAsync(filtro, query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
                .ConfigureAwait(false);

        DecisaoDaProtecaoDto[] items = [.. itens.Select(static a => a.ToDecisaoDto())];
        return Result<ListarDecisoesDaProtecaoResult>.Success(new ListarDecisoesDaProtecaoResult(items, anterior, proximo));
    }

    private static FiltroDasDecisoesDaProtecao Filtro(ListarDecisoesDaProtecaoQuery query, AtorDoAto quemConsulta, List<FieldError> erros)
    {
        List<AtoDaProtecao> decisoes = Decisoes(query.Atos, erros);
        CadastroDoCatalogo? cadastro = Cadastro(query.Cadastro, erros);
        ConferirPeriodo(query.De, query.Ate, erros);

        Result<string?> busca = BuscaPedida.Validar(query.Busca);
        if (busca.IsFailure)
        {
            erros.Add(new("busca", busca.Error!));
        }

        return new FiltroDasDecisoesDaProtecao(
            decisoes, cadastro, query.De, query.Ate, busca.Value, query.Minhas ? (quemConsulta.Emissor, quemConsulta.Sub) : null);
    }

    private static List<AtoDaProtecao> Decisoes(IEnumerable<string> tokens, List<FieldError> erros)
    {
        List<AtoDaProtecao> decisoes = [];
        foreach (string token in tokens)
        {
            if (AtosDaProtecao.TryAnalisar(token, out AtoDaProtecao ato) && AtosDaProtecao.Decisoes.Contains(ato))
            {
                decisoes.Add(ato);
            }
            else
            {
                erros.Add(AtoInvalido());
            }
        }

        return decisoes;
    }

    private static FieldError AtoInvalido() => new("ato", new DomainError(
        ProtecaoDoCatalogoErrorCodes.AtoInvalido, "A decisão deve ser APROVACAO, AJUSTE_RECOMENDADO ou REPROVACAO."));

    private static CadastroDoCatalogo? Cadastro(string? pedido, List<FieldError> erros)
    {
        if (pedido is null)
        {
            return null;
        }

        bool valido = CadastrosDoCatalogo.TryAnalisar(pedido, out CadastroDoCatalogo analisado);
        if (!valido)
        {
            erros.Add(CadastroInvalido());
        }

        return valido ? analisado : null;
    }

    private static FieldError CadastroInvalido() => new("cadastro", new DomainError(
        ProtecaoDoCatalogoErrorCodes.CadastroInvalido,
        $"O cadastro do item deve ser {CadastrosDaProtecaoDoCatalogo.TipoDocumento} ou {CadastrosDaProtecaoDoCatalogo.Fato}."));

    private static void ConferirPeriodo(DateOnly? de, DateOnly? ate, List<FieldError> erros)
    {
        if (de is { } inicio && ate is { } fim && inicio > fim)
        {
            erros.Add(new("de", new DomainError(
                ProtecaoDoCatalogoErrorCodes.PeriodoInvalido, "A data inicial não pode ser posterior à data final.")));
        }
    }
}
