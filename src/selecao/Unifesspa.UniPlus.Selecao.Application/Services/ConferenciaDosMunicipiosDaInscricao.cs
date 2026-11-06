namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A conferência no Geo dos municípios que o candidato respondeu: o município existe e é da UF que a restrição de
/// municípios da UF do campo cita. Confere só o município de campo que a avaliação deu como aplicável — o de campo oculto é
/// descartado no envio, sem recusa —, no nível superior e nos subitens de grupo. A UF vem da avaliação, e por isso a
/// conferência roda depois dela; o município de campo cuja UF a avaliação não resolveu não é conferido, porque o campo
/// já tem recusa.
/// </summary>
public static class ConferenciaDosMunicipiosDaInscricao
{
    /// <param name="camposRecusados">Os campos que já têm recusa: o município deles não é conferido.</param>
    public static async Task<ResultadoDaConferenciaDosMunicipios> ConferirAsync(
        DefinicaoFormulario definicao,
        RespostasAdmitidas admitidas,
        AvaliacaoFormulario avaliacao,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlySet<string> camposRecusados,
        IConferenciaEnderecoGeo conferencia,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(admitidas);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(camposRecusados);
        ArgumentNullException.ThrowIfNull(conferencia);

        Dictionary<string, string> ufDoMunicipio = UfDeCadaCampoDeMunicipio(definicao, admitidas);
        ApuracaoDosMunicipios apuracao = new(conferencia, camposRecusados);

        foreach (AvaliacaoItem item in avaliacao.Itens.Where(i => ufDoMunicipio.ContainsKey(i.FatoCodigo)))
        {
            await apuracao.ConferirAsync(
                item.FatoCodigo, respostas, avaliacao.Fatos, avaliacao.Fatos, ufDoMunicipio[item.FatoCodigo], cancellationToken).ConfigureAwait(false);
        }

        foreach (AvaliacaoGrupo grupo in avaliacao.Grupos.Where(g => g.Estado != EstadoFato.NaoAplicavel && grupos.ContainsKey(g.Codigo)))
        {
            // O id repetido já é recusa de forma do envio; a conferência fica com a primeira ocorrência de cada id.
            Dictionary<string, OcorrenciaDeGrupoInscricao> respondidaPorId = [];
            foreach (OcorrenciaDeGrupoInscricao ocorrenciaRespondida in grupos[grupo.Codigo])
            {
                respondidaPorId.TryAdd(ocorrenciaRespondida.Id, ocorrenciaRespondida);
            }

            foreach (AvaliacaoOcorrencia ocorrencia in grupo.Ocorrencias.Where(o => respondidaPorId.ContainsKey(o.Id)))
            {
                foreach (AvaliacaoItem subitem in ocorrencia.Itens.Where(i => ufDoMunicipio.ContainsKey(i.FatoCodigo)))
                {
                    await apuracao.ConferirAsync(
                        subitem.FatoCodigo, respondidaPorId[ocorrencia.Id].Respostas, ocorrencia.Fatos, avaliacao.Fatos,
                        ufDoMunicipio[subitem.FatoCodigo], cancellationToken, caminho: $"{grupo.Codigo}.{ocorrencia.Id}").ConfigureAwait(false);
                }
            }
        }

        return apuracao.Resultado();
    }

    /// <summary>
    /// O fato da UF de cada campo de município: a restrição de municípios da UF é obrigatória em todo campo de município,
    /// no nível superior e nos subitens de grupo.
    /// </summary>
    private static Dictionary<string, string> UfDeCadaCampoDeMunicipio(DefinicaoFormulario definicao, RespostasAdmitidas admitidas)
    {
        Dictionary<string, string> ufDoMunicipio = new(StringComparer.Ordinal);
        foreach (DefinicaoItem item in definicao.Etapas.SelectMany(static e => e.Itens.Concat(e.Grupos.SelectMany(static g => g.Subitens))))
        {
            if (admitidas.Definicoes.TryGetValue(item.FatoCodigo, out DefinicaoDaResposta? definicaoDaResposta) && definicaoDaResposta.EhMunicipio
                && item.Restricoes.OfType<MunicipiosDaUf>().FirstOrDefault() is { } daUf)
            {
                ufDoMunicipio[item.FatoCodigo] = daUf.FatoUf;
            }
        }

        return ufDoMunicipio;
    }

    private sealed class ApuracaoDosMunicipios(IConferenciaEnderecoGeo conferencia, IReadOnlySet<string> camposRecusados)
    {
        private readonly List<FieldError> _recusas = [];
        private readonly Dictionary<string, string> _nomes = new(StringComparer.Ordinal);
        private readonly Dictionary<(string Codigo, string Uf), Result<string>> _conferidos = [];

        public DomainError? GeoIndisponivel { get; private set; }

        public ResultadoDaConferenciaDosMunicipios Resultado() => new(_recusas, _nomes, GeoIndisponivel);

        /// <summary>Confere o município do campo; com o Geo já fora do ar, as demais conferências não valem a espera.</summary>
        public async Task ConferirAsync(
            string fatoCodigo,
            IReadOnlyDictionary<string, JsonElement> respostasDoEscopo,
            IReadOnlyDictionary<string, FatoResolvido> fatosDoEscopo,
            IReadOnlyDictionary<string, FatoResolvido> fatosDoCandidato,
            string fatoDaUf,
            CancellationToken cancellationToken,
            string? caminho = null)
        {
            string campo = caminho is null ? fatoCodigo : $"{caminho}.{fatoCodigo}";
            if (GeoIndisponivel is not null
                || camposRecusados.Contains(campo)
                || !fatosDoEscopo.TryGetValue(fatoCodigo, out FatoResolvido? fato) || fato.Estado == EstadoFato.NaoAplicavel
                || !respostasDoEscopo.TryGetValue(fatoCodigo, out JsonElement resposta) || ValorDeMunicipio.Codigo(resposta) is not { } codigo
                || UfResolvida(fatoDaUf, fatosDoEscopo, fatosDoCandidato) is not { } uf)
            {
                return;
            }

            if (!_conferidos.TryGetValue((codigo, uf), out Result<string>? conferido))
            {
                conferido = await conferencia.ConferirMunicipioAsync(codigo, uf, cancellationToken).ConfigureAwait(false);
                _conferidos[(codigo, uf)] = conferido;
            }

            if (conferido.IsSuccess)
            {
                _nomes[codigo] = conferido.Value!;
            }
            else if (conferido.Error!.Code == ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel)
            {
                GeoIndisponivel = conferido.Error;
            }
            else
            {
                _recusas.Add(new FieldError(campo, conferido.Error));
            }
        }

        private static string? UfResolvida(
            string fatoDaUf, IReadOnlyDictionary<string, FatoResolvido> fatosDoEscopo, IReadOnlyDictionary<string, FatoResolvido> fatosDoCandidato) =>
            (fatosDoEscopo.TryGetValue(fatoDaUf, out FatoResolvido? doEscopo) ? doEscopo : fatosDoCandidato.GetValueOrDefault(fatoDaUf))
                is { Estado: EstadoFato.Resolvido, Valor: { ValueKind: JsonValueKind.String } valor }
                ? valor.GetString()
                : null;
    }
}

/// <summary>
/// O que a conferência dos municípios apurou: as recusas por campo, o nome que o Geo deu a cada município conferido, pelo
/// código, e o erro do Geo quando ele ficou fora do ar.
/// </summary>
public sealed record ResultadoDaConferenciaDosMunicipios(
    IReadOnlyList<FieldError> Recusas, IReadOnlyDictionary<string, string> Nomes, DomainError? GeoIndisponivel);
