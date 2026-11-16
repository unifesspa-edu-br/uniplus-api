namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>
/// Cada área informada precisa ser uma das cinco, uma vez só, e nenhuma pode faltar. O erro vai no campo
/// da área (<c>areas[i].codigo</c>, <c>.peso</c>, <c>.corte</c>), e a falta, em <c>areas</c>, nomeando
/// os códigos ausentes. Toda violação independente se acumula.
/// </summary>
internal sealed class ConferenciaDasAreasDoPeso
{
    private const int EscalaPeso = 2;
    private const int EscalaCorte = 3;
    private const int TamanhoMaximoDoCodigoEcoado = 40;

    /// <summary>
    /// Teto de itens aceitos em <c>areas</c>: acima dele a lista é recusada inteira, sem um erro por item —
    /// uma entrada pequena não pode gerar resposta desproporcional.
    /// </summary>
    private const int MaximoDeItensInformados = 20;

    private static readonly string AreasAceitas =
        string.Join(", ", PesoAreaEnem.Areas.Select(static a => $"{a.Codigo} ({a.Rotulo})"));

    private readonly List<FieldError> _erros;
    private readonly List<AreaValidada> _validas = [];
    private readonly HashSet<string> _vistas = new(StringComparer.Ordinal);

    private ConferenciaDasAreasDoPeso(List<FieldError> erros)
    {
        _erros = erros;
    }

    public static IReadOnlyList<AreaValidada> Conferir(List<FieldError> erros, IReadOnlyList<AreaInformada>? areas)
    {
        if (areas is { Count: > MaximoDeItensInformados })
        {
            erros.Add(new("areas", new DomainError(
                PesoAreaEnemErrorCodes.AreasEmExcesso,
                $"Informe as cinco áreas, uma vez cada; vieram {areas.Count} itens.")));
            return [];
        }

        return new ConferenciaDasAreasDoPeso(erros).ConferirTodas(areas ?? []);
    }

    private static decimal Arredondar(decimal valor, int escala) =>
        Math.Round(valor, escala, MidpointRounding.ToEven);

    private List<AreaValidada> ConferirTodas(IReadOnlyList<AreaInformada> areas)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            ConferirArea($"areas[{i}]", areas[i]);
        }

        string[] faltando = [.. PesoAreaEnem.Areas.Select(static a => a.Codigo).Where(c => !_vistas.Contains(c))];
        RecusarAsQueFaltam(faltando);
        RecusarPesosTodosZerados(faltando);
        return _validas;
    }

    private void ConferirArea(string campo, AreaInformada informada)
    {
        AreaEnemDescrita? descrita = Identificar(campo, informada.Codigo?.Trim());
        bool repetida = RecusarSeRepetida(campo, descrita);

        // A área repetida continua conhecida: peso e corte dela são conferidos pela regra da própria
        // área, só não entram na linha.
        string nomeDaArea = descrita?.Rotulo ?? "esta área";
        int errosAntes = _erros.Count;
        ConferirPeso(campo, informada.Peso, nomeDaArea);
        ConferirCorte(campo, informada.Corte, nomeDaArea);

        if (descrita is not null && !repetida && _erros.Count == errosAntes)
        {
            _validas.Add(new AreaValidada(descrita, Arredondar(informada.Peso, EscalaPeso), ArredondarCorte(informada.Corte)));
        }
    }

    private AreaEnemDescrita? Identificar(string campo, string? codigo)
    {
        if (codigo is not null && PesoAreaEnem.OrdemPorCodigo.TryGetValue(codigo, out int ordem))
        {
            return PesoAreaEnem.Areas[ordem];
        }

        Recusar($"{campo}.codigo", PesoAreaEnemErrorCodes.AreaForaDoDominio, string.IsNullOrEmpty(codigo)
            ? $"Informe o código da área. Aceitas: {AreasAceitas}."
            : $"\"{CaracteresInvisiveis.ParaEco(codigo, TamanhoMaximoDoCodigoEcoado)}\" não é uma área do Peso por Área. Aceitas: {AreasAceitas}.");
        return null;
    }

    private bool RecusarSeRepetida(string campo, AreaEnemDescrita? descrita)
    {
        if (descrita is null || _vistas.Add(descrita.Codigo))
        {
            return false;
        }

        Recusar($"{campo}.codigo", PesoAreaEnemErrorCodes.AreaRepetida,
            $"A área {descrita.Codigo} ({descrita.Rotulo}) foi informada mais de uma vez.");
        return true;
    }

    private void ConferirPeso(string campo, decimal peso, string nomeDaArea)
    {
        if (peso < 0)
        {
            Recusar($"{campo}.peso", PesoAreaEnemErrorCodes.PesoNegativo,
                $"O peso informado para {nomeDaArea} não pode ser negativo.");
        }
        else if (peso > PesoAreaEnem.PesoMaximo)
        {
            Recusar($"{campo}.peso", PesoAreaEnemErrorCodes.PesoExcedeMaximo,
                $"O peso informado para {nomeDaArea} não pode exceder {PesoAreaEnem.PesoMaximo}.");
        }
    }

    private void ConferirCorte(string campo, decimal? corte, string nomeDaArea)
    {
        if (corte < 0)
        {
            Recusar($"{campo}.corte", PesoAreaEnemErrorCodes.CorteNegativo,
                $"O corte informado para {nomeDaArea} não pode ser negativo.");
        }
        else if (corte > PesoAreaEnem.CorteMaximo)
        {
            Recusar($"{campo}.corte", PesoAreaEnemErrorCodes.CorteExcedeMaximo,
                $"O corte informado para {nomeDaArea} não pode exceder {PesoAreaEnem.CorteMaximo} (nota máxima de uma área do ENEM).");
        }
    }

    private static decimal? ArredondarCorte(decimal? corte) =>
        corte is null ? null : Arredondar(corte.Value, EscalaCorte);

    private void RecusarAsQueFaltam(string[] faltando)
    {
        if (faltando.Length > 0)
        {
            Recusar("areas", PesoAreaEnemErrorCodes.AreaFaltando,
                $"Informe o peso das cinco áreas; faltam: {string.Join(", ", faltando)}.");
        }
    }

    // A nota da etapa divide pela soma dos pesos: com tudo zerado (inclusive o que só zera depois de
    // arredondar à escala gravada) não há média a calcular.
    private void RecusarPesosTodosZerados(string[] faltando)
    {
        if (faltando.Length == 0 && _validas.Count == PesoAreaEnem.Areas.Count && _validas.All(static a => a.Peso == 0m))
        {
            Recusar("areas", PesoAreaEnemErrorCodes.PesosSomamZero,
                "Informe ao menos uma área com peso maior que zero; a nota da etapa é a média ponderada por esses pesos.");
        }
    }

    private void Recusar(string campo, string codigo, string mensagem) =>
        _erros.Add(new(campo, new DomainError(codigo, mensagem)));
}
