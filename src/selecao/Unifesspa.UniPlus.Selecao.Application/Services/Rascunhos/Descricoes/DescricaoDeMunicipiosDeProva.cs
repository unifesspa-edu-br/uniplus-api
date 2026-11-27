namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// O nome de cada município de prova vem do Geo, uma consulta por código distinto: o limite conta códigos distintos, porque é o custo
/// da consulta ao Geo que ele protege, e o município repetido no rascunho salvo é consultado uma só vez. Acima do limite de
/// municípios distintos, ou com o Geo fora do
/// ar, nenhum nome sai: a exportação segue válida, pois o código IBGE já identifica o município. O município que o Geo
/// não conhece fica sem nome, e o código IBGE declarado com mais de uma UF fica sem nome em todas as ocorrências.
/// </summary>
public sealed class DescricaoDeMunicipiosDeProva(
    ConferenciaDosMunicipiosNoGeo conferencia, IOptions<ProvaPresencialOptions> options) : IDescricaoDeCadastro
{
    private static readonly IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> SemNomes =
        new Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>();

    public string Cadastro => CamposDeContextoDoDocumento.MunicipiosDeProva;

    public async Task<IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>>> LerAsync(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        if (codigos.Select(static municipio => municipio.Codigo).Distinct(StringComparer.Ordinal).Count() > options.Value.LimiteDeMunicipios)
        {
            return SemNomes;
        }

        MunicipioDeProva[] municipios = [.. SemCodigoComMaisDeUmaUf(codigos).Select(static (municipio, posicao) => new MunicipioDeProva(municipio.Codigo, municipio.Complemento!, null, posicao))];
        Result<MunicipiosConferidosNoGeo> conferidos = await conferencia.ConferirAsync(municipios, cancellationToken).ConfigureAwait(false);
        return conferidos.IsFailure ? SemNomes : NomesDos(municipios, conferidos.Value!);
    }

    // A conferência guarda um resultado por código IBGE: com UFs diferentes, o nome de uma ocorrência valeria para a outra.
    private static IEnumerable<(string Codigo, string? Complemento)> SemCodigoComMaisDeUmaUf(
        IReadOnlyCollection<(string Codigo, string? Complemento)> codigos)
    {
        HashSet<string> comMaisDeUmaUf = [.. codigos
            .GroupBy(static municipio => municipio.Codigo, StringComparer.Ordinal)
            .Where(static grupo => grupo.Select(static municipio => municipio.Complemento).Distinct().Count() > 1)
            .Select(static grupo => grupo.Key)];
        return codigos.Where(municipio => !comMaisDeUmaUf.Contains(municipio.Codigo));
    }

    private static Dictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> NomesDos(
        MunicipioDeProva[] municipios, MunicipiosConferidosNoGeo conferidos) =>
        municipios.Select(municipio => (Municipio: municipio, Nome: conferidos.NomeConferido(municipio.CodigoIbge)))
            .Where(static conferido => conferido.Nome is not null)
            .ToDictionary(
                static conferido => (conferido.Municipio.CodigoIbge, (string?)conferido.Municipio.Uf),
                static conferido => (IReadOnlyList<object?>)[conferido.Nome]);
}
