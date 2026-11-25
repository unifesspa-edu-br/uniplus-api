namespace Unifesspa.UniPlus.Regras.Leituras;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras do formulário — predicado, obrigatoriedade, restrição de valor e impedimento — na forma de
/// leitura, a mesma para item, grupo e termo, e a volta dessa forma à de entrada. A leitura das regras de
/// entrada continua em <see cref="EntradaDeRegras"/>: aqui só a forma muda.
/// </summary>
public static class LeituraDeRegras
{
    public static ObrigatoriedadeDto ToDto(this Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new ObrigatoriedadeDto(
            obrigatoriedade.Tipo.Codigo(),
            obrigatoriedade.Predicado?.ToDto());
    }

    public static RestricaoValorDto ToDto(this RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        string tipo = restricao.Tipo.Codigo();
        return restricao switch
        {
            FaixaNumerica faixa => new RestricaoValorDto(tipo, faixa.Minimo, faixa.Maximo, null, null, null, null),
            TamanhoTexto tamanho => new RestricaoValorDto(tipo, tamanho.Minimo, tamanho.Maximo, null, null, null, null),
            OpcoesPermitidas opcoes => new RestricaoValorDto(tipo, null, null, opcoes.ParaOpcoesCondicionadas(), null, null, null),
            OpcoesDasRespostas respostas => new RestricaoValorDto(tipo, null, null, null, respostas.Fatos, null, null),
            MunicipiosDaUf daUf => new RestricaoValorDto(tipo, null, null, null, [daUf.FatoUf], null, null),
            FaixaDeData faixaDeData => faixaDeData.ParaRestricao(tipo),
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao.Tipo, "Tipo de restrição sem forma de leitura."),
        };
    }

    private static List<OpcoesCondicionadasDto> ParaOpcoesCondicionadas(this OpcoesPermitidas opcoes) =>
        [.. opcoes.Entradas.Select(static e =>
            new OpcoesCondicionadasDto(e.Quando?.ToDto(), [.. e.Valores.Order(StringComparer.Ordinal)]))];

    private static RestricaoValorDto ParaRestricao(this FaixaDeData faixa, string tipo) =>
        new(tipo, null, null, null, null, faixa.Minima?.ToDto(), faixa.Maxima?.ToDto());

    public static LimiteDeDataDto ToDto(this LimiteDeData limite)
    {
        ArgumentNullException.ThrowIfNull(limite);
        return limite.Ancora == AncoraDoLimiteDeData.DataFixa
            ? new LimiteDeDataDto(RestricaoValorJson.AncoraDataFixa, limite.Data, null, null)
            : new LimiteDeDataDto(RestricaoValorJson.AncoraEnvio, null, limite.Anos, limite.Dias);
    }

    public static ImpedimentoDto ToDto(this Impedimento impedimento)
    {
        ArgumentNullException.ThrowIfNull(impedimento);
        return new(impedimento.Quando.ToDto(), impedimento.Mensagem);
    }

    /// <summary>O predicado na forma de leitura; o predicado sem cláusula vira a lista vazia.</summary>
    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>> ToDto(this PredicadoDnf predicado)
    {
        ArgumentNullException.ThrowIfNull(predicado);
        return [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoPrecondicaoDto>)
            [.. c.Condicoes.Select(static d => new CondicaoPrecondicaoDto(d.Fato, d.Operador.Codigo(), d.Valor))])];
    }

    public static ApresentacaoDoCampoDto ToDto(this ApresentacaoDoCampo apresentacao)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        return new(apresentacao.Rotulo, apresentacao.TipoDeCampo.Codigo(), apresentacao.Ajuda, apresentacao.PedirConfirmacao);
    }

    public static RegrasDoCampoDto ToDto(this RegrasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return new(
            regras.Exibicao?.ToDto(),
            regras.Obrigatoriedade.ToDto(),
            [.. regras.Restricoes.Select(static r => r.ToDto())],
            regras.Impedimento?.ToDto());
    }

    public static ExibicaoEObrigatoriedadeDto ToDto(this ExibicaoEObrigatoriedade regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return new(regras.Exibicao?.ToDto(), regras.Obrigatoriedade.ToDto());
    }

    public static OcorrenciasDoGrupoDto ToDto(this OcorrenciasDoGrupo ocorrencias)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);
        return new(ocorrencias.Minimo, ocorrencias.Maximo, ocorrencias.IncluiCandidato);
    }

    /// <summary>O predicado na forma de entrada; nulo continua nulo e a lista vazia continua vazia.</summary>
    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? ParaEntrada(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoDto>>? predicado) =>
        predicado is null
            ? null
            : [.. predicado.Select(static c => (IReadOnlyList<CondicaoPrecondicaoInput>)
                [.. c.Select(static d => new CondicaoPrecondicaoInput(d.Fato, d.Operador, d.Valor))])];

    public static ObrigatoriedadeInput? ParaEntrada(ObrigatoriedadeDto? obrigatoriedade) =>
        obrigatoriedade is null ? null : new(obrigatoriedade.Tipo, ParaEntrada(obrigatoriedade.Predicado));

    public static ImpedimentoInput? ParaEntrada(ImpedimentoDto? impedimento) =>
        impedimento is null ? null : new(ParaEntrada(impedimento.Quando), impedimento.Mensagem);

    public static RestricaoValorInput ParaEntrada(RestricaoValorDto restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        return new(
            restricao.Tipo,
            restricao.Minimo,
            restricao.Maximo,
            restricao.Entradas is null ? null : [.. restricao.Entradas.Select(static e => new OpcoesCondicionadasInput(ParaEntrada(e.Quando), e.Valores))],
            restricao.Fatos,
            ParaEntrada(restricao.DataMinima),
            ParaEntrada(restricao.DataMaxima));
    }

    private static LimiteDeDataInput? ParaEntrada(LimiteDeDataDto? limite) =>
        limite is null ? null : new(limite.Ancora, limite.Data, limite.Anos, limite.Dias);
}
