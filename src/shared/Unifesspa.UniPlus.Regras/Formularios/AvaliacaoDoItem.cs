namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class AvaliacaoDoItem
{
    private readonly string _paginaCodigo;
    private readonly bool _paginaConcluida;
    private readonly DateOnly? _diaDoEnvio;
    private readonly IReadOnlyDictionary<string, FatoResolvido> _fatos;
    private readonly DefinicaoItem _item;
    private readonly Ternario _visivel;
    private readonly Ternario _obrigatorio;
    private readonly OpcoesVigentes? _opcoes;

    internal AvaliacaoDoItem(string paginaCodigo, bool paginaConcluida, DateOnly? diaDoEnvio,
        IReadOnlyDictionary<string, FatoResolvido> fatos, DefinicaoItem item, Ternario visivelDoContentor)
    {
        _paginaCodigo = paginaCodigo;
        _paginaConcluida = paginaConcluida;
        _diaDoEnvio = diaDoEnvio;
        _fatos = fatos;
        _item = item;
        _visivel = AvaliadorFormulario.E(visivelDoContentor, item.Exibicao?.Avaliar(fatos) ?? Ternario.Verdadeiro);
        _obrigatorio = AvaliadorFormulario.ObrigatorioSeVisivel(_visivel, item.Obrigatoriedade, fatos);
        _opcoes = _visivel == Ternario.Falso ? null : item.OpcoesVigentes(fatos);
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
        (IReadOnlyList<RestricaoValor> violadas, bool indeterminada) =
            _item.ConferirResposta(resposta, new ContextoDaResposta(_fatos, _diaDoEnvio));
        return violadas.Count == 0
            ? Resultado(indeterminada ? FatoResolvido.Indeterminado() : FatoResolvido.Resolvido(resposta), [])
            : ResultadoSemResposta(violadas);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) ResultadoSemResposta(IReadOnlyList<RestricaoValor> violadas)
    {
        FatoResolvido semResposta = AvaliacaoDaColeta.EstadoSemResposta(_obrigatorio, _paginaConcluida) == EstadoFato.NaoInformado
            ? FatoResolvido.NaoInformado() : FatoResolvido.Indeterminado();
        return Resultado(semResposta, violadas);
    }

    private (FatoResolvido Fato, AvaliacaoItem Avaliacao) Resultado(FatoResolvido fato, IReadOnlyList<RestricaoValor> violadas) =>
        (fato, new AvaliacaoItem(_item.FatoCodigo, _paginaCodigo, _visivel, _obrigatorio, violadas, _item.Impedido(fato, _fatos), _opcoes));
}
