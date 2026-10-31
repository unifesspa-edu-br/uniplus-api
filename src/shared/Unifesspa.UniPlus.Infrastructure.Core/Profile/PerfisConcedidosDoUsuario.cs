namespace Unifesspa.UniPlus.Infrastructure.Core.Profile;

using System.Globalization;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Monta a lista de perfis que a pessoa tem por concessão vigente, com o escopo de cada um. O token
/// só identifica a pessoa (ADR-0152): o que ela pode fazer vem das concessões gravadas.
/// </summary>
internal static class PerfisConcedidosDoUsuario
{
    internal const string EscopoUnidade = "unidade";
    internal const string EscopoInstituicao = "instituicao";

    private static readonly StringComparer ComparadorDeTexto =
        StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    /// <summary>
    /// Falha quando o token não traz emissor e <i>subject</i> válidos: sem eles não há de quem listar.
    /// </summary>
    internal static async Task<Result<IReadOnlyList<PerfilConcedidoResponse>>> ListarAsync(
        string? emissor,
        string? subject,
        IConcessoesAtivasDoSujeito concessoes,
        IUnidadeReader unidades,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result<UsuarioRef> pessoa = UsuarioRef.From(emissor, subject);
        if (pessoa.IsFailure)
        {
            return Result<IReadOnlyList<PerfilConcedidoResponse>>.Failure(pessoa.Error!);
        }

        IReadOnlyList<ConcessaoAtiva> gravadas = await concessoes
            .ListarAsync(pessoa.Value!, cancellationToken)
            .ConfigureAwait(false);

        List<ConcessaoAtiva> vigentes = [.. gravadas.Where(c => c.VigenteEm(agora))];

        Dictionary<Guid, UnidadeView?> unidadesLidas = [];
        foreach (Guid unidadeId in vigentes.Where(static c => c.UnidadeId is not null).Select(static c => c.UnidadeId!.Value).Distinct())
        {
            unidadesLidas[unidadeId] = await unidades
                .ObterPorIdAsync(unidadeId, cancellationToken)
                .ConfigureAwait(false);
        }

        // OrderBy é estável: no empate final vale a ordem da porta, que é a da data da concessão.
        IReadOnlyList<PerfilConcedidoResponse> perfis =
        [
            .. vigentes
                .Select(c => (Concessao: c, Unidade: c.UnidadeId is { } id ? unidadesLidas[id] : null))
                .OrderBy(static x => x.Concessao.PerfilNome, ComparadorDeTexto)
                .ThenBy(static x => x.Concessao.Institucional ? 0 : 1)
                .ThenBy(static x => x.Unidade is null ? 1 : 0)
                .ThenBy(static x => x.Unidade?.Sigla, ComparadorDeTexto)
                .Select(static x => new PerfilConcedidoResponse(
                    x.Concessao.ConcessaoId,
                    x.Concessao.PerfilId,
                    x.Concessao.PerfilNome,
                    x.Concessao.Institucional ? EscopoInstituicao : EscopoUnidade,
                    x.Concessao.UnidadeId,
                    x.Unidade?.Sigla,
                    x.Unidade?.Nome,
                    x.Concessao.ValidaAte)),
        ];

        return Result<IReadOnlyList<PerfilConcedidoResponse>>.Success(perfis);
    }
}
