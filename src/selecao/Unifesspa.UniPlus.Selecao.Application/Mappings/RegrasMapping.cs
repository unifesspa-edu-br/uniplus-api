namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// As regras do formulário — predicado, obrigatoriedade e restrição de valor — na forma de leitura, a
/// mesma para item e termo.
/// </summary>
internal static class RegrasMapping
{
    public static ObrigatoriedadeDto ToDto(this Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new ObrigatoriedadeDto(
            PredicadoDnfJson.ParaToken(obrigatoriedade.Tipo),
            obrigatoriedade.Predicado?.ToDto());
    }

    public static RestricaoValorDto ToDto(this RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        string tipo = RestricaoValorJson.ParaToken(restricao.Tipo);
        return restricao switch
        {
            FaixaNumerica faixa => new RestricaoValorDto(tipo, faixa.Minimo, faixa.Maximo, null, null),
            TamanhoTexto tamanho => new RestricaoValorDto(tipo, tamanho.Minimo, tamanho.Maximo, null, null),
            OpcoesPermitidas opcoes => new RestricaoValorDto(tipo, null, null, [.. opcoes.Entradas.Select(static e =>
                new OpcoesCondicionadasDto(e.Quando?.ToDto(), [.. e.Valores.Order(StringComparer.Ordinal)]))], null),
            OpcoesDasRespostas respostas => new RestricaoValorDto(tipo, null, null, null, respostas.Fatos),
            MunicipiosDaUf daUf => new RestricaoValorDto(tipo, null, null, null, [daUf.FatoUf]),
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao.Tipo, "Tipo de restrição sem forma de leitura."),
        };
    }

    public static ImpedimentoDto ToDto(this Impedimento impedimento)
    {
        ArgumentNullException.ThrowIfNull(impedimento);
        return new(impedimento.Quando.ToDto(), impedimento.Mensagem);
    }

    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> ToDto(this PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoPrecondicaoDto>)
            [.. c.Condicoes.Select(static d => new CondicaoPrecondicaoDto(d.Fato, d.Operador.ToCodigo(), d.Valor))])];
    }
}
