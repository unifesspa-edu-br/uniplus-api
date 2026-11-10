namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As divergências da governança, da mais recente para a mais antiga.</summary>
public static class ListarDivergenciasDaGovernancaQueryHandler
{
    public static async Task<Result<IReadOnlyList<DivergenciaDaGovernancaDto>>> Handle(
        ListarDivergenciasDaGovernancaQuery query, IVerificadorDeAcesso verificador, IReclassificacaoRepository reclassificacoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reclassificacoes);

        bool? pode = await AcessoDaVersao.PodeVerAGovernancaAsync(verificador, cancellationToken).ConfigureAwait(false);
        if (pode is not true)
        {
            return Result<IReadOnlyList<DivergenciaDaGovernancaDto>>.Failure(
                pode is null ? PerfilHandlerErros.AutorNaoIdentificado() : ReclassificacaoErros.SemPermissaoNaEtapa());
        }

        IReadOnlyList<DivergenciaDaGovernanca> divergencias = await reclassificacoes.ListarDivergenciasAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> nomes = await reclassificacoes
            .ObterNomesDosPerfisAsync([.. divergencias.Select(static d => d.PerfilId).Distinct()], cancellationToken).ConfigureAwait(false);

        return Result<IReadOnlyList<DivergenciaDaGovernancaDto>>.Success(
        [.. divergencias.Select(d => new DivergenciaDaGovernancaDto(
            d.Id, d.PerfilId, nomes.GetValueOrDefault(d.PerfilId, string.Empty), d.Codigo, d.Tipo.ToCodigo(), d.De, d.Para, d.Instante))]);
    }
}
