namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

/// <summary>
/// Handler da <see cref="ObterRegrasDerivacaoNormativasQuery"/>: leitura pura que recorta a matriz
/// normativa para o que o processo oferta.
/// </summary>
/// <remarks>
/// O recorte é necessário, não cosmético: <c>PUT …/regras-derivacao</c> recusa a regra que
/// contribui código fora das modalidades ofertadas, de modo que devolver a matriz inteira daria ao
/// cliente uma proposta que o próprio servidor rejeitaria em seguida.
/// <para>
/// Sobrando regra nenhuma — processo que ainda não declarou quadro de vagas, ou que oferta só
/// modalidade de outro ramo — a resposta é uma lista vazia, e não uma configuração sem regra: o
/// agregado recusa configuração vazia, e propô-la seria propor o que não se pode gravar.
/// </para>
/// </remarks>
public static class ObterRegrasDerivacaoNormativasQueryHandler
{
    public static async Task<IReadOnlyList<ConfiguracaoDerivacaoDto>?> Handle(
        ObterRegrasDerivacaoNormativasQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterComConfiguracaoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return null;
        }

        HashSet<string> ofertadas =
        [
            .. processo.DistribuicaoVagas
                .SelectMany(static distribuicao => distribuicao.Modalidades)
                .Select(static modalidade => modalidade.Codigo),
        ];

        List<RegraDerivacaoDto> regras = [];
        int ordem = 0;
        foreach (RegraDerivacao regra in RegrasDerivacaoModalidadeLei12711.Construir().Regras)
        {
            if (!ofertadas.Contains(regra.Contribui))
            {
                continue;
            }

            regras.Add(new RegraDerivacaoDto(
                ordem++,
                regra.Contribui,
                regra.EhAncora ? null : [.. regra.Quando.Clausulas.Select(ComoClausula)]));
        }

        return regras.Count == 0
            ? []
            : [new ConfiguracaoDerivacaoDto(RegrasDerivacaoModalidadeLei12711.CodigoFato, regras)];
    }

    private static IReadOnlyList<CondicaoDerivacaoDto> ComoClausula(ClausulaDnf clausula) =>
        [.. clausula.Condicoes.Select(static condicao =>
            new CondicaoDerivacaoDto(condicao.Fato, condicao.Operador.ToCodigo(), condicao.Valor))];
}
