namespace Unifesspa.UniPlus.Regras.Entradas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

using Linhas = System.Collections.Generic.List<(int Clausula, Unifesspa.UniPlus.Regras.ValueObjects.CondicaoDnf Condicao)>;

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

        Result<List<(int Clausula, CondicaoDnf Condicao)>> linhas = Condicoes(clausulas);
        if (linhas.IsFailure)
        {
            return Result<PredicadoDnf?>.ValidationFailure(linhas.Errors);
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas.Value!);
        return predicado.IsSuccess ? Result<PredicadoDnf?>.Success(predicado.Value) : Result<PredicadoDnf?>.Failure(predicado.Error!);
    }

    /// <summary>
    /// As condições da entrada, cada uma com a posição da cláusula. É a única leitura de condição vinda do pedido: acumula
    /// as recusas de todas as cláusulas e condições, cada uma no caminho <c>[cláusula][condição].campo</c>. A lista nula
    /// é a ausência de condição.
    /// </summary>
    public static Result<List<(int Clausula, CondicaoDnf Condicao)>> Condicoes(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput?>?>? clausulas)
    {
        Linhas linhas = [];
        List<FieldError> erros = [];
        for (int c = 0; c < (clausulas?.Count ?? 0); c++)
        {
            LerClausula(clausulas![c], c, linhas, erros);
        }

        return erros.Count > 0
            ? Result<List<(int Clausula, CondicaoDnf Condicao)>>.ValidationFailure(erros)
            : Result<List<(int Clausula, CondicaoDnf Condicao)>>.Success(linhas);
    }

    private static void LerClausula(IReadOnlyList<CondicaoPrecondicaoInput?>? condicoes, int clausula, Linhas linhas, List<FieldError> erros)
    {
        if (condicoes is not { Count: > 0 })
        {
            erros.Add(new($"[{clausula}]", new DomainError("ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição.")));
            return;
        }

        LerCondicoes(condicoes, clausula, linhas, erros);
    }

    private static void LerCondicoes(IReadOnlyList<CondicaoPrecondicaoInput?> condicoes, int clausula, Linhas linhas, List<FieldError> erros)
    {
        for (int i = 0; i < condicoes.Count; i++)
        {
            Result<CondicaoDnf> lida = LerCondicao(condicoes[i]);
            erros.AddRange(lida.Errors.Select(erro => erro.NoCaminhoDe($"[{clausula}][{i}]")));
            if (lida.IsSuccess)
            {
                linhas.Add((clausula, lida.Value!));
            }
        }
    }

    private static Result<CondicaoDnf> LerCondicao(CondicaoPrecondicaoInput? condicao) =>
        condicao is null
            ? Result<CondicaoDnf>.ValidationFailure([new FieldError("", new DomainError(PredicadoDnfErrorCodes.CondicaoNula, "O predicado contém uma condição nula."))])
            : CondicaoDnf.CriarInformada(condicao.Fato, condicao.Operador, condicao.Valor);

    /// <summary>
    /// A obrigatoriedade do token e do predicado: <c>SEMPRE</c> e <c>NUNCA</c> sem predicado,
    /// <c>QUANDO</c> com ele; qualquer outra combinação é <see langword="null"/>, e quem chama
    /// recusa com o código da sua entidade.
    /// </summary>
    public static Obrigatoriedade? Obrigatoriedade(string? token, PredicadoDnf? predicado) =>
        CatalogoDe<TipoObrigatoriedade>.TentarObter(token, out TipoObrigatoriedade tipo)
            ? Formularios.Obrigatoriedade.DaForma(tipo, predicado)
            : null;

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
            return Result<Impedimento?>.ValidationFailure(quando.Errors);
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
        return RestricoesDeValor.TipoDeclarado(input.Tipo) switch
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
                [.. c.Condicoes.Select(static k => new CondicaoPrecondicaoInput(k.Fato, k.Operador.Codigo(), k.Valor))])];

    /// <summary>O token da obrigatoriedade; o predicado do <c>QUANDO</c> vai à parte (<see cref="ParaEntrada(PredicadoDnf?)"/>).</summary>
    public static string ParaEntrada(Formularios.Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return obrigatoriedade.Tipo.Codigo();
    }

    /// <summary>
    /// A apresentação da entrada com o tipo de campo já lido. A entrada ausente e o rótulo ausente ficam sem
    /// texto, e a recusa deles é da forma do item.
    /// </summary>
    public static ApresentacaoDoCampo Apresentacao(ApresentacaoDoCampoInput? entrada, TipoRenderizacao tipoRenderizacao) =>
        new(entrada?.Rotulo ?? string.Empty, tipoRenderizacao, entrada?.Ajuda, entrada?.PedirConfirmacao ?? false);

    /// <summary>A obrigatoriedade na forma da entrada: o token e, quando há, o predicado.</summary>
    public static ObrigatoriedadeInput ObrigatoriedadeNaEntrada(Formularios.Obrigatoriedade obrigatoriedade)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        return new(ParaEntrada(obrigatoriedade), ParaEntrada(obrigatoriedade.Predicado));
    }

    /// <summary>As ocorrências na forma da entrada.</summary>
    public static OcorrenciasDoGrupoInput ParaEntrada(OcorrenciasDoGrupo ocorrencias)
    {
        ArgumentNullException.ThrowIfNull(ocorrencias);
        return new(ocorrencias.Minimo, ocorrencias.IncluiCandidato, ocorrencias.Maximo);
    }

    /// <summary>A exibição e a obrigatoriedade na forma da entrada: a obrigatoriedade leva o token e, quando há, o predicado.</summary>
    public static ExibicaoEObrigatoriedadeInput ParaEntrada(ExibicaoEObrigatoriedade regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return new(ParaEntrada(regras.Exibicao), ObrigatoriedadeNaEntrada(regras.Obrigatoriedade));
    }

    /// <summary>A apresentação do campo na forma da entrada, com o tipo de campo no código canônico.</summary>
    public static ApresentacaoDoCampoInput ParaEntrada(ApresentacaoDoCampo apresentacao)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        return new(apresentacao.Rotulo, apresentacao.TipoRenderizacao.Codigo(), apresentacao.Ajuda, apresentacao.PedirConfirmacao);
    }

    /// <summary>As regras do campo na forma da entrada: a obrigatoriedade leva o token e, quando há, o predicado.</summary>
    public static RegrasDoCampoInput ParaEntrada(RegrasDoCampo regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return new(
            ParaEntrada(regras.Exibicao),
            ObrigatoriedadeNaEntrada(regras.Obrigatoriedade),
            [.. regras.Restricoes.Select(ParaEntrada)],
            ParaEntrada(regras.Impedimento));
    }

    /// <summary>A restrição de valor na forma da entrada.</summary>
    public static RestricaoValorInput ParaEntrada(RestricaoValor restricao)
    {
        ArgumentNullException.ThrowIfNull(restricao);
        string tipo = restricao.Tipo.Codigo();
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
