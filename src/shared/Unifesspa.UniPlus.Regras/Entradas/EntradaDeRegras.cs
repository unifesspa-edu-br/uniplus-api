namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma das regras do formulário vindas da escrita — predicado, obrigatoriedade e restrição de
/// valor —, a mesma para item e termo, no processo e no modelo. A semântica (fatos citáveis, operador e valor do domínio) é conferida depois,
/// contra o catálogo.
/// </summary>
public static class EntradaDeRegras
{
    /// <summary>O predicado da entrada; nulo ou sem cláusula é ausência de condição.</summary>
    public static Result<PredicadoDnf?> Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas)
    {
        if (clausulas is null || clausulas.Count == 0)
        {
            return Result<PredicadoDnf?>.Success(null);
        }

        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = CondicoesPorClausula(clausulas);
        if (linhas.IsFailure)
        {
            return Result<PredicadoDnf?>.Failure(linhas.Error!);
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas.Value!);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    private static Result<List<(int Clausula, CondicaoDnf Condicao)>> CondicoesPorClausula(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> clausulas)
    {
        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        for (int c = 0; c < clausulas.Count; c++)
        {
            Result<List<CondicaoDnf>> condicoes = CondicoesDaClausula(clausulas[c]);
            if (condicoes.IsFailure)
            {
                return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Failure(condicoes.Error!);
            }

            linhas.AddRange(condicoes.Value!.Select(condicao => (c, condicao)));
        }

        return Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(linhas);
    }

    private static Result<List<CondicaoDnf>> CondicoesDaClausula(IReadOnlyList<CondicaoPrecondicaoInput>? condicoes)
    {
        if (condicoes is not { Count: > 0 })
        {
            return Result<List<CondicaoDnf>>.Failure(new DomainError("ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
        }

        return CriarCondicoes(condicoes);
    }

    private static Result<List<CondicaoDnf>> CriarCondicoes(IReadOnlyList<CondicaoPrecondicaoInput> condicoes)
    {
        List<CondicaoDnf> criadas = [];
        foreach (CondicaoPrecondicaoInput? condicao in condicoes)
        {
            Result<CondicaoDnf> criada = CriarCondicao(condicao);
            if (criada.IsFailure)
            {
                return Result<List<CondicaoDnf>>.Failure(criada.Error!);
            }

            criadas.Add(criada.Value!);
        }

        return Result<List<CondicaoDnf>>.Success(criadas);
    }

    private static Result<CondicaoDnf> CriarCondicao(CondicaoPrecondicaoInput? condicao) =>
        condicao is null
            ? Result<CondicaoDnf>.Failure(new DomainError(PredicadoDnfErrorCodes.CondicaoNula, "O predicado contém uma condição nula."))
            : CondicaoDnf.Criar(condicao.Fato, OperadorCodigo.FromCodigo(condicao.Operador), condicao.Valor);

    /// <summary>
    /// A obrigatoriedade do token e do predicado: <c>SEMPRE</c> e <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele; qualquer outra combinação é <see langword="null"/>, e quem chama
    /// recusa com o código da sua entidade.
    /// </summary>
    public static Obrigatoriedade? Obrigatoriedade(string? token, PredicadoDnf? predicado) =>
        (PredicadoDnfJson.TipoDoToken(token), predicado) switch
        {
            (TipoObrigatoriedade.Sempre, null) => Formularios.Obrigatoriedade.Sempre,
            (TipoObrigatoriedade.Nunca, null) => Formularios.Obrigatoriedade.Nunca,
            (TipoObrigatoriedade.Quando, { } quando) => Formularios.Obrigatoriedade.Quando(quando),
            _ => null,
        };

    /// <summary>
    /// O impedimento da entrada; nulo quando o campo não tem. A condição é obrigatória; a mensagem e
    /// as cláusulas sobre o próprio campo são conferidas pela forma do item.
    /// </summary>
    public static Result<Impedimento?> Impedimento(ImpedimentoInput? input)
    {
        if (input is null)
        {
            return Result<Impedimento?>.Success(null);
        }

        Result<PredicadoDnf?> quando = Predicado(input.Quando);
        if (quando.IsFailure)
        {
            return Result<Impedimento?>.Failure(quando.Error!);
        }

        return ImpedimentoSobre(quando.Value, input.Mensagem);
    }

    private static Result<Impedimento?> ImpedimentoSobre(PredicadoDnf? condicao, string? mensagem) =>
        condicao is null
            ? Result<Impedimento?>.Failure(new DomainError(
                ItemFormularioErrorCodes.ImpedimentoSemCondicao, "O impedimento tem a condição sobre a resposta do campo."))
            : Result<Impedimento?>.Success(new Formularios.Impedimento(condicao, FormaDoItem.TextoOpcional(mensagem) ?? string.Empty));

    /// <summary>O impedimento na forma da entrada; nulo quando o campo não tem.</summary>
    public static ImpedimentoInput? ParaEntrada(Formularios.Impedimento? impedimento) =>
        impedimento is null ? null : new(ParaEntrada(impedimento.Quando), impedimento.Mensagem);

    /// <summary>A restrição de valor da entrada, pelo tipo; os valores e fatos citados são conferidos depois.</summary>
    public static Result<RestricaoValor> Restricao(RestricaoValorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        IReadOnlyList<string> fatos = input.Fatos ?? [];
        return RestricaoValorJson.TipoDoToken(input.Tipo) switch
        {
            TipoRestricaoValor.FaixaNumerica => RestricoesDeValor.Faixa(input.Minimo, input.Maximo),
            TipoRestricaoValor.TamanhoTexto => LerTamanhoDeTexto(input.Minimo, input.Maximo),
            TipoRestricaoValor.OpcoesPermitidas => Opcoes(input.Entradas),
            TipoRestricaoValor.OpcoesDasRespostas => RestricoesDeValor.DasRespostas(fatos),
            TipoRestricaoValor.MunicipiosDaUf => RestricoesDeValor.DaUf(fatos),
            TipoRestricaoValor.FaixaDeData => LerFaixaDeData(input.DataMinima, input.DataMaxima),
            _ => TipoDeRestricaoDesconhecido(),
        };
    }

    private static Result<RestricaoValor> TipoDeRestricaoDesconhecido() =>
        Result<RestricaoValor>.Failure(new DomainError(
            RestricaoValorErrorCodes.TipoDesconhecido,
            "O tipo da restrição é FAIXA_NUMERICA, TAMANHO_TEXTO, OPCOES_PERMITIDAS, OPCOES_DAS_RESPOSTAS, MUNICIPIOS_DA_UF ou FAIXA_DE_DATA."));

    private static Result<RestricaoValor> LerTamanhoDeTexto(decimal? minimo, decimal? maximo) =>
        RestricaoValorJson.Inteiro(minimo, out int? tamanhoMinimo) && RestricaoValorJson.Inteiro(maximo, out int? tamanhoMaximo)
            ? RestricoesDeValor.Tamanho(tamanhoMinimo, tamanhoMaximo)
            : Result<RestricaoValor>.Failure(new DomainError(
                RestricaoValorErrorCodes.LimitesIncoerentes, "Os limites de tamanho de texto são números inteiros."));

    private static Result<RestricaoValor> LerFaixaDeData(LimiteDeDataInput? minima, LimiteDeDataInput? maxima)
    {
        Result<LimiteDeData?> lidaMinima = LerLimite(minima);
        Result<LimiteDeData?> lidaMaxima = LerLimite(maxima);
        return lidaMinima.IsFailure || lidaMaxima.IsFailure
            ? Result<RestricaoValor>.Failure((lidaMinima.Error ?? lidaMaxima.Error)!)
            : RestricoesDeValor.FaixaDeData(lidaMinima.Value, lidaMaxima.Value);
    }

    private static Result<LimiteDeData?> LerLimite(LimiteDeDataInput? input)
    {
        if (input is null)
        {
            return Result<LimiteDeData?>.Success(null);
        }

        Result<LimiteDeData> lido = RestricoesDeValor.Limite(input.Ancora, input.Data, input.Anos, input.Dias);
        return lido.IsSuccess ? Result<LimiteDeData?>.Success(lido.Value) : Result<LimiteDeData?>.Failure(lido.Error!);
    }

    /// <summary>
    /// O envio que a simulação supõe; nulo quando ela não o informa. A zona é procurada na base de fusos
    /// do ambiente, e a que ela não reconhece é recusada no fuso. O instante que fica perto demais das pontas
    /// do calendário, como o primeiro dele, para os limites da faixa de data se contarem dele é recusado no
    /// instante. A ausência do instante no corpo nem chega aqui: é recusada na leitura.
    /// </summary>
    public static Result<EnvioDasRespostas?> Envio(EnvioSimuladoInput? input)
    {
        if (input is null)
        {
            return Result<EnvioDasRespostas?>.Success(null);
        }

        return Fuso(input.Fuso) is { } fuso
            ? EnvioDentroDoCalendario(new EnvioDasRespostas(input.Instante, fuso))
            : EnvioRecusado(
                "envio.fuso", FormularioPortavelErrorCodes.FusoDoEnvioDesconhecido, $"A zona '{input.Fuso}' do envio não é reconhecida.");
    }

    private static Result<EnvioDasRespostas?> EnvioDentroDoCalendario(EnvioDasRespostas envio) =>
        LimiteDeData.ComportaOsDeslocamentos(envio.Dia)
            ? Result<EnvioDasRespostas?>.Success(envio)
            : EnvioRecusado(
                "envio.instante",
                FormularioPortavelErrorCodes.InstanteDoEnvioForaDoCalendario,
                "O instante do envio falta ou fica fora do calendário que os limites da faixa de data alcançam.");

    private static Result<EnvioDasRespostas?> EnvioRecusado(string campo, string codigo, string mensagem) =>
        Result<EnvioDasRespostas?>.ValidationFailure([new FieldError(campo, new DomainError(codigo, mensagem))]);

    private static TimeZoneInfo? Fuso(string? zona)
    {
        if (string.IsNullOrWhiteSpace(zona))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zona);
        }
        catch (Exception excecao) when (excecao is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>O limite da faixa de data na forma da entrada; nulo quando o limite falta.</summary>
    public static LimiteDeDataInput? ParaEntrada(LimiteDeData? limite) => limite switch
    {
        null => null,
        { Ancora: AncoraDoLimiteDeData.DataFixa } => new(RestricaoValorJson.AncoraDataFixa, Data: limite.Data),
        _ => new(RestricaoValorJson.AncoraEnvio, Anos: limite.Anos, Dias: limite.Dias),
    };

    private static Result<RestricaoValor> Opcoes(IReadOnlyList<OpcoesCondicionadasInput>? entradas)
    {
        List<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lidas = [];
        foreach (OpcoesCondicionadasInput? entrada in entradas ?? [])
        {
            Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> lida = GrupoDeOpcoes(entrada);
            if (lida.IsFailure)
            {
                return Result<RestricaoValor>.Failure(lida.Error!);
            }

            lidas.Add(lida.Value);
        }

        return RestricoesDeValor.Opcoes(lidas);
    }

    private static Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)> GrupoDeOpcoes(OpcoesCondicionadasInput? entrada)
    {
        if (entrada is null)
        {
            return Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)>.Failure(new DomainError(
                RestricaoValorErrorCodes.OpcoesVazias, "As opções permitidas contêm um grupo nulo."));
        }

        Result<PredicadoDnf?> quando = Predicado(entrada.Quando);
        return quando.IsFailure
            ? Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)>.Failure(quando.Error!)
            : Result<(PredicadoDnf? Quando, IReadOnlyCollection<string> Valores)>.Success((quando.Value, entrada.Valores ?? []));
    }

    /// <summary>
    /// O predicado na forma da entrada — o OU de cláusulas, cada uma o E de condições —, para a
    /// leitura que volta à edição; nulo quando não há condição.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? ParaEntrada(PredicadoDnf? predicado) =>
        predicado is null
            ? null
            : [.. predicado.Clausulas.Select(static c => (IReadOnlyList<CondicaoPrecondicaoInput>)
                [.. c.Condicoes.Select(static k => new CondicaoPrecondicaoInput(k.Fato, k.Operador.ToCodigo(), k.Valor))])];

    /// <summary>O token da obrigatoriedade; o predicado do <c>QUANDO</c> vai à parte (<see cref="ParaEntrada(PredicadoDnf?)"/>).</summary>
    public static string ParaEntrada(Formularios.Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return PredicadoDnfJson.ParaToken(obrigatoriedade.Tipo);
    }

    /// <summary>A restrição de valor na forma da entrada.</summary>
    public static RestricaoValorInput ParaEntrada(RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        string tipo = RestricaoValorJson.ParaToken(restricao.Tipo);
        return restricao switch
        {
            FaixaNumerica faixa => new(tipo, faixa.Minimo, faixa.Maximo),
            TamanhoTexto tamanho => new(tipo, tamanho.Minimo, tamanho.Maximo),
            OpcoesPermitidas opcoes => new(tipo, Entradas: [.. opcoes.Entradas.Select(static e =>
                new OpcoesCondicionadasInput(ParaEntrada(e.Quando), [.. e.Valores.Order(StringComparer.Ordinal)]))]),
            OpcoesDasRespostas respostas => new(tipo, Fatos: respostas.Fatos),
            MunicipiosDaUf daUf => new(tipo, Fatos: [daUf.FatoUf]),
            FaixaDeData faixaDeData => new(tipo, DataMinima: ParaEntrada(faixaDeData.Minima), DataMaxima: ParaEntrada(faixaDeData.Maxima)),
            _ => throw new ArgumentOutOfRangeException(nameof(restricao), restricao.Tipo, "Restrição de valor fora do vocabulário."),
        };
    }
}
