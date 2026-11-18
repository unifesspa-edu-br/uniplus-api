namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.ObjectModel;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>A avaliação e os fatos apurados para o envio, com os documentos que integram o comprovante.</summary>
public sealed record ApuracaoDoEnvio
{
    public ApuracaoDoEnvio(ResultadoDaAvaliacaoDaInscricao avaliacao, IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoesDosDerivados, IReadOnlyList<ArquivoConfirmado> arquivosConfirmados,
        MunicipiosConferidos? municipios = null)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(definicoesDosDerivados);
        Avaliacao = avaliacao;
        Derivados = new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>(derivados, StringComparer.Ordinal));
        DefinicoesDosDerivados = new ReadOnlyDictionary<string, DefinicaoDaResposta>(new Dictionary<string, DefinicaoDaResposta>(definicoesDosDerivados, StringComparer.Ordinal));
        ArquivosConfirmados = arquivosConfirmados is null ? null! : Array.AsReadOnly(arquivosConfirmados.ToArray());
        Municipios = municipios;
    }

    public ResultadoDaAvaliacaoDaInscricao Avaliacao { get; }
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; }
    public IReadOnlyDictionary<string, DefinicaoDaResposta> DefinicoesDosDerivados { get; }
    public IReadOnlyList<ArquivoConfirmado> ArquivosConfirmados { get; }
    public MunicipiosConferidos? Municipios { get; }

    public IEnumerable<FieldError> RecusasDosDerivados() =>
        PreenchimentoDoCandidato.ConferirValores(Derivados, new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(),
            DefinicoesDosDerivados, new Dictionary<string, IReadOnlySet<string>>());

    public IReadOnlyList<DocumentoDoComprovante> DocumentosDoComprovante() =>
        Services.DocumentosDoComprovante.Compor(Avaliacao.Documentos, ArquivosConfirmados);
}
