namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Onde a leitura de uma inscrição deposita as respostas, cada uma no lugar que a linha manda: as informadas da raiz, as
/// derivadas, as de cada ocorrência de grupo — e a definição de cada fato, que a primeira linha lida do fato decide.
/// </summary>
internal sealed class RespostasLidasDaInscricao
{
    private readonly Dictionary<string, JsonElement> _informadas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _derivadas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DefinicaoDaResposta> _definicoes = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Dictionary<string, JsonElement>> _porOcorrencia;
    private readonly IReadOnlyList<OcorrenciaDeGrupoDaInscricaoRecord> _ocorrencias;

    public RespostasLidasDaInscricao(IReadOnlyList<OcorrenciaDeGrupoDaInscricaoRecord> ocorrencias)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);

        _ocorrencias = ocorrencias;
        _porOcorrencia = ocorrencias.ToDictionary(
            static o => o.Id, static _ => new Dictionary<string, JsonElement>(StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, JsonElement> Informadas => _informadas;

    public IReadOnlyDictionary<string, JsonElement> Derivadas => _derivadas;

    public IReadOnlyDictionary<string, DefinicaoDaResposta> Definicoes => _definicoes;

    public void AcrescentarFato(
        Guid? ocorrenciaId, OrigemDaResposta origem, string fatoCodigo, JsonElement valor, DefinicaoDaResposta definicao)
    {
        Destino(ocorrenciaId, origem)[fatoCodigo] = valor;
        _definicoes.TryAdd(fatoCodigo, definicao);
    }

    public void AcrescentarEndereco(EnderecoDaInscricaoRecord endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);

        AcrescentarFato(
            endereco.OcorrenciaId,
            endereco.Origem,
            endereco.FatoCodigo,
            EnderecoGravadoDaInscricao.Declarado(endereco).ParaJson(),
            new DefinicaoDaResposta(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false));
    }

    public Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos() =>
        _ocorrencias
            .OrderBy(static o => o.Ordem)
            .GroupBy(static o => o.GrupoCodigo, StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                    [.. g.Select(o => new OcorrenciaDeGrupoInscricao(o.MembroId, _porOcorrencia[o.Id]))],
                StringComparer.Ordinal);

    private Dictionary<string, JsonElement> Destino(Guid? ocorrenciaId, OrigemDaResposta origem) =>
        ocorrenciaId is { } id ? _porOcorrencia[id] : origem == OrigemDaResposta.Calculada ? _derivadas : _informadas;
}
