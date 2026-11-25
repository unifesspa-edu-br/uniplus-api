namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliadorDaOcorrencia
{
    private readonly string _paginaCodigo;
    private readonly bool _paginaConcluida;
    private readonly DateOnly? _diaDoEnvio;
    private readonly OcorrenciaRespondida _ocorrencia;
    private readonly Dictionary<string, FatoResolvido> _contexto;
    private readonly Dictionary<string, FatoResolvido> _fatos = new(StringComparer.Ordinal);
    private readonly List<AvaliacaoItem> _itens = [];

    internal AvaliadorDaOcorrencia(string paginaCodigo, bool paginaConcluida, ContextoDaResposta contexto,
        OcorrenciaRespondida ocorrencia)
    {
        _paginaCodigo = paginaCodigo;
        _paginaConcluida = paginaConcluida;
        _diaDoEnvio = contexto.DiaDoEnvio;
        _ocorrencia = ocorrencia;
        _contexto = new(contexto.Fatos, StringComparer.Ordinal);
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
        AvaliadorDoItem avaliadorDoItem = new(_paginaCodigo, _paginaConcluida, new ContextoDaResposta(_contexto, _diaDoEnvio), subitem, Ternario.Verdadeiro);
        (FatoResolvido fato, AvaliacaoItem avaliacao) = avaliadorDoItem.Avaliar(resposta);
        _contexto[subitem.FatoCodigo] = fato;
        _fatos[subitem.FatoCodigo] = fato;
        _itens.Add(avaliacao);
    }
}
