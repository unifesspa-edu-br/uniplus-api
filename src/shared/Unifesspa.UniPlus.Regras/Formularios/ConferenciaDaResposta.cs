namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;

internal sealed class ConferenciaDaResposta
{
    internal List<RestricaoValor> Violadas { get; } = [];

    internal bool Indeterminada { get; private set; }

    internal void Conferir(RestricaoValor restricao, JsonElement resposta, ContextoDaResposta contexto)
    {
        Ternario resultado = restricao.Avaliar(resposta, contexto);
        if (resultado == Ternario.Falso)
        {
            Violadas.Add(restricao);
        }
        else if (resultado == Ternario.Indeterminado)
        {
            Indeterminada = true;
        }
    }
}
