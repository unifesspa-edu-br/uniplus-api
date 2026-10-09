namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Handler da <see cref="ObterRegrasDerivacaoNormativasQuery"/>: leitura pura que recorta a matriz
/// normativa para o que o processo oferta.
/// </summary>
/// <remarks>
/// O recorte é necessário, não cosmético: <c>PUT …/regras-derivacao</c> recusa a regra que
/// contribui código fora das modalidades ofertadas, de modo que devolver a matriz inteira daria ao
/// cliente uma proposta que o próprio servidor rejeitaria em seguida.
/// <para>
/// O processo que não oferta nenhuma cota da lei recebe a matriz sem cotas: nela a reserva de pessoa
/// com deficiência não cita escola pública nem opção pelas cotas, fatos que ele não coleta.
/// </para>
/// <para>
/// Sobrando regra nenhuma — processo que ainda não declarou quadro de vagas, ou que oferta só
/// modalidade de outro ramo — a resposta é uma lista vazia, e não uma configuração sem regra: o
/// agregado recusa configuração vazia, e propô-la seria propor o que não se pode gravar.
/// </para>
/// <para>
/// A matriz cita derivados por regra, como o egresso de escola pública, que se deriva da origem
/// escolar. O que o processo ainda não deriva vem junto, com as regras padrão do catálogo, para a
/// proposta ser gravável tal como chega.
/// </para>
/// </remarks>
public static class ObterRegrasDerivacaoNormativasQueryHandler
{
    public static async Task<IReadOnlyList<ConfiguracaoDerivacaoDto>?> Handle(
        ObterRegrasDerivacaoNormativasQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(fatoCandidatoReader);

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

        RegrasDerivacaoFato matriz = ModalidadesFederaisLei12711.Codigos.Any(ofertadas.Contains)
            ? RegrasDerivacaoModalidadeLei12711.Construir()
            : RegrasDerivacaoModalidadeLei12711.ConstruirSemCotasDaLei();

        List<RegraDerivacaoDto> regras = [];
        int ordem = 0;
        foreach (RegraDerivacao regra in matriz.Regras)
        {
            // A matriz normativa é da modalidade, categórica: toda regra contribui uma modalidade.
            if (regra.Contribui is not { } modalidade || !ofertadas.Contains(modalidade))
            {
                continue;
            }

            regras.Add(new RegraDerivacaoDto(
                ordem++,
                modalidade,
                regra.EhAncora ? null : [.. regra.Quando.Clausulas.Select(ComoClausula)]));
        }

        if (regras.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, IReadOnlyList<RegraDerivacao>> regrasPadrao =
            await fatoCandidatoReader.ListarRegrasPadraoAsync(cancellationToken).ConfigureAwait(false);
        HashSet<string> jaDerivados = [.. processo.RegrasDerivacao.Select(static r => r.CodigoFato)];
        List<ConfiguracaoDerivacaoDto> derivados = [.. regras
            .SelectMany(static r => r.Quando ?? []).SelectMany(static c => c).Select(static c => c.Fato)
            .Distinct(StringComparer.Ordinal)
            .Where(fato => !jaDerivados.Contains(fato) && regrasPadrao.ContainsKey(fato))
            .Order(StringComparer.Ordinal)
            .Select(fato => new ConfiguracaoDerivacaoDto(fato, [.. regrasPadrao[fato].Select(static (regra, i) =>
                new RegraDerivacaoDto(i, regra.Contribui, regra.EhAncora ? null : [.. regra.Quando.Clausulas.Select(ComoClausula)]))]))];

        return [.. derivados, new ConfiguracaoDerivacaoDto(RegrasDerivacaoModalidadeLei12711.CodigoFato, regras)];
    }

    private static IReadOnlyList<CondicaoDerivacaoDto> ComoClausula(ClausulaDnf clausula) =>
        [.. clausula.Condicoes.Select(static condicao =>
            new CondicaoDerivacaoDto(condicao.Fato, condicao.Operador.ToCodigo(), condicao.Valor))];
}
