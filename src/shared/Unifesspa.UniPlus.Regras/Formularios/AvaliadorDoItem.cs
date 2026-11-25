namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliadorDoItem
{
    private readonly string _paginaCodigo;
    private readonly bool _paginaConcluida;
    private readonly ContextoDaResposta _contexto;
    private readonly DefinicaoItem _item;
    private readonly Ternario _visivel;
    private readonly Ternario _obrigatorio;
    private readonly OpcoesVigentes? _opcoes;

    internal AvaliadorDoItem(string paginaCodigo, bool paginaConcluida, ContextoDaResposta contexto,
        DefinicaoItem item, Ternario visibilidadeHerdada)
    {
        _paginaCodigo = paginaCodigo;
        _paginaConcluida = paginaConcluida;
        _contexto = contexto;
        _item = item;
        _visivel = AvaliadorFormulario.E(visibilidadeHerdada, item.Exibicao?.Avaliar(contexto.Fatos) ?? Ternario.Verdadeiro);
        _obrigatorio = AvaliadorFormulario.ObrigatorioSeVisivel(_visivel, item.Obrigatoriedade, contexto.Fatos);
        _opcoes = _visivel == Ternario.Falso ? null : item.OpcoesVigentes(contexto.Fatos);
    }

    internal (FatoResolvido Fato, AvaliacaoItem Avaliacao) Avaliar(JsonElement? resposta)
    {
        if (_visivel == Ternario.Falso)
        {
            return Resultado(FatoResolvido.NaoAplicavel(), []);
        }
        if (_visivel == Ternario.Indeterminado)
        {
            return Resultado(FatoResolvido.Indeterminado(), []);
        }
        return AvaliarResposta(resposta);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) AvaliarResposta(JsonElement? resposta)
    {
        if (resposta is { } respondida && !RespostaDeCampo.EstaVazia(respondida))
        {
            return ConferirResposta(respondida);
        }
        return ResultadoSemResposta([]);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) ConferirResposta(JsonElement resposta)
    {
        ConferenciaDaResposta conferencia = _item.ConferirResposta(resposta, _contexto);
        return conferencia.Violadas.Count == 0
            ? Resultado(conferencia.Indeterminada ? FatoResolvido.Indeterminado() : FatoResolvido.Resolvido(resposta), [])
            : ResultadoSemResposta(conferencia.Violadas);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) ResultadoSemResposta(IReadOnlyList<RestricaoValor> violadas)
    {
        FatoResolvido semResposta = RespostaDeCampo.EstadoSemResposta(_obrigatorio, _paginaConcluida) == EstadoFato.NaoInformado
            ? FatoResolvido.NaoInformado() : FatoResolvido.Indeterminado();
        return Resultado(semResposta, violadas);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) Resultado(FatoResolvido fato, IReadOnlyList<RestricaoValor> violadas) =>
        (fato, new AvaliacaoItem(_item.FatoCodigo, _paginaCodigo, _visivel, _obrigatorio, violadas, _item.Impedido(fato, _contexto.Fatos), _opcoes));
}
