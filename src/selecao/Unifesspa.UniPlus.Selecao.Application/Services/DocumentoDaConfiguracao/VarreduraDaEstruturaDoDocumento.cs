namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text;
using System.Text.Json;

using Kernel.Results;

/// <summary>
/// Percorre o texto do documento uma vez, sem montar a árvore, e devolve o que a estrutura recusa: o JSON malformado, a
/// raiz que não é objeto, a profundidade ou a quantidade de valores acima do limite, o caractere nulo e a metade de um par
/// substituto UTF-16 em texto ou em nome de propriedade, e cada objeto que repete uma propriedade. A repetição só se enxerga aqui porque a leitura da árvore, mesmo recusando a duplicata, não diz em que objeto ela está.
/// </summary>
internal ref struct VarreduraDaEstruturaDoDocumento
{
    private readonly List<NivelDaVarredura> _niveisComNomeRepetido;
    private readonly List<FieldError> _causasDoTexto;
    private Utf8JsonReader _leitor;
    private NivelDaVarredura? _atual;
    private int _profundidade;
    private int _niveisAbertos;
    private int _valoresLidos;

    public VarreduraDaEstruturaDoDocumento(ReadOnlySpan<byte> utf8)
    {
        _leitor = new Utf8JsonReader(utf8, new JsonReaderOptions());
        _niveisComNomeRepetido = [];
        _causasDoTexto = [];
    }

    public IReadOnlyList<FieldError> Varrer()
    {
        try
        {
            FieldError? recusa = LerOsTokens();
            return recusa is null ? [.. _causasDoTexto, .. PropriedadesRepetidas()] : [recusa];
        }
        catch (JsonException)
        {
            return [CausasDaForma.Malformado()];
        }
    }

    private FieldError? LerOsTokens()
    {
        while (_leitor.Read())
        {
            FieldError? recusa = _valoresLidos == 0 && _leitor.TokenType != JsonTokenType.StartObject
                ? CausasDaForma.RaizNaoEhObjeto()
                : LerOToken();
            if (recusa is not null)
            {
                return recusa;
            }
        }

        return null;
    }

    private FieldError? LerOToken() => _leitor.TokenType switch
    {
        JsonTokenType.PropertyName => RegistrarNome(),
        JsonTokenType.StartObject or JsonTokenType.StartArray => AbrirNivel(),
        JsonTokenType.EndObject or JsonTokenType.EndArray => FecharNivel(),
        _ => ContarValor(),
    };

    private FieldError? RegistrarNome()
    {
        string? nome = TextoDoToken();
        if (DefeitoDoNome(nome) is { } defeito)
        {
            _causasDoTexto.Add(defeito);
        }

        // O nome que não se lê como texto segue pela escrita bruta: distingue um do outro sem guardar o texto inválido.
        if (_atual!.PassouARepetirNome(nome ?? Encoding.UTF8.GetString(_leitor.ValueSpan)))
        {
            _niveisComNomeRepetido.Add(_atual);
        }

        return null;
    }

    private FieldError? AbrirNivel()
    {
        string segmento = _atual?.SegmentoDoProximoValor() ?? string.Empty;
        _valoresLidos++;
        _atual = new NivelDaVarredura(_atual, segmento, _leitor.TokenType == JsonTokenType.StartArray, _niveisAbertos++);
        _profundidade++;

        return _profundidade > LimitesDoDocumento.ProfundidadeMaxima
            ? CausasDaForma.ProfundidadeAcimaDoLimite(_atual.Ponteiro())
            : ConferirQuantidadeDeValores();
    }

    private FieldError? FecharNivel()
    {
        _atual = _atual!.Superior;
        _profundidade--;
        return null;
    }

    private FieldError? ContarValor()
    {
        string segmento = _atual!.SegmentoDoProximoValor();
        _valoresLidos++;
        if (DefeitoDoValor(segmento) is { } defeito)
        {
            _causasDoTexto.Add(defeito);
        }

        return ConferirQuantidadeDeValores();
    }

    // Só o texto escrito com escape (\u0000, \uD800) chega ao caractere nulo ou à metade de um par substituto: o byte nulo cru
    // e o UTF-8 inválido o próprio leitor recusa como malformado.
    private readonly FieldError? DefeitoDoValor(string segmento)
    {
        if (!_leitor.ValueIsEscaped)
        {
            return null;
        }

        string ponteiro = PonteiroJson.Acrescentar(_atual!.Ponteiro(), segmento);
        return TextoDoToken() switch
        {
            null => CausasDaForma.SubstitutoIsoladoNoValor(ponteiro),
            string texto when TemCaractereNulo(texto) => CausasDaForma.CaractereNuloNoValor(ponteiro),
            _ => null,
        };
    }

    private readonly FieldError? DefeitoDoNome(string? nome) => nome switch
    {
        null => CausasDaForma.SubstitutoIsoladoNoNomeDaPropriedade(_atual!.Ponteiro()),
        _ when TemCaractereNulo(nome) => CausasDaForma.CaractereNuloNoNomeDaPropriedade(_atual!.Ponteiro()),
        _ => null,
    };

    private static bool TemCaractereNulo(string texto) => texto.Contains('\0', StringComparison.Ordinal);

    // O leitor só recusa a metade de um par substituto ao entregar o texto, e o faz lançando; aqui ela vira ausência de texto.
    private readonly string? TextoDoToken()
    {
        try
        {
            return _leitor.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private readonly FieldError? ConferirQuantidadeDeValores() =>
        _valoresLidos > LimitesDoDocumento.QuantidadeMaximaDeNos ? CausasDaForma.QuantidadeDeNosAcimaDoLimite() : null;

    private readonly IReadOnlyList<FieldError> PropriedadesRepetidas() =>
    [
        .. _niveisComNomeRepetido.OrderBy(static nivel => nivel.OrdemDeAbertura)
            .Select(static nivel => CausasDaForma.PropriedadeRepetida(nivel.Ponteiro())),
    ];
}
