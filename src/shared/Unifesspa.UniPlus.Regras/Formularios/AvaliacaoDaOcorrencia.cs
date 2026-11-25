namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliacaoDaOcorrencia
{
    private readonly string _etapaCodigo;
    private readonly bool _etapaConcluida;
    private readonly DateOnly? _diaDoEnvio;
    private readonly OcorrenciaRespondida _ocorrencia;
    private readonly Dictionary<string, FatoResolvido> _contexto;
    private readonly Dictionary<string, FatoResolvido> _fatos = new(StringComparer.Ordinal);
    private readonly List<AvaliacaoItem> _itens = [];

    internal AvaliacaoDaOcorrencia(string etapaCodigo, bool etapaConcluida, DateOnly? diaDoEnvio,
        IReadOnlyDictionary<string, FatoResolvido> fatos, OcorrenciaRespondida ocorrencia)
    {
        _etapaCodigo = etapaCodigo;
        _etapaConcluida = etapaConcluida;
        _diaDoEnvio = diaDoEnvio;
        _ocorrencia = ocorrencia;
        _contexto = new(fatos, StringComparer.Ordinal);
    }

    internal AvaliacaoOcorrencia Avaliar(IEnumerable<DefinicaoItem> subitens)
    {
        foreach (DefinicaoItem subitem in subitens)
        {
            AvaliarSubitem(subitem);
        }
        EstadoFato estado = _fatos.Values.Any(static fato => fato.Estado == EstadoFato.Indeterminado) ? EstadoFato.Indeterminado : EstadoFato.Resolvido;
        return new AvaliacaoOcorrencia(_ocorrencia.Id, _itens, _fatos, estado);
    }

    private void AvaliarSubitem(DefinicaoItem subitem)
    {
        JsonElement? resposta = _ocorrencia.Respostas.TryGetValue(subitem.FatoCodigo, out JsonElement valor) ? valor : null;
        AvaliacaoDoItem avaliacaoDoItem = new(_etapaCodigo, _etapaConcluida, _diaDoEnvio, _contexto, subitem, Ternario.Verdadeiro);
        (FatoResolvido fato, AvaliacaoItem avaliacao) = avaliacaoDoItem.Avaliar(resposta);
        _contexto[subitem.FatoCodigo] = fato;
        _fatos[subitem.FatoCodigo] = fato;
        _itens.Add(avaliacao);
    }
}
