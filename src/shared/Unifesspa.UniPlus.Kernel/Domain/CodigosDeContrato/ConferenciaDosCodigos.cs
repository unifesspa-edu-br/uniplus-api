namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using System.Reflection;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// A conferência dos códigos de um enum: cada membro, na ordem em que o enum o declara, é conferido, e cada defeito é
/// acumulado com o nome do membro responsável.
/// </summary>
internal sealed class ConferenciaDosCodigos
{
    private readonly string _tipo;
    private readonly List<string> _defeitos = [];
    private readonly Dictionary<string, string> _codigos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _rotulos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<object, string> _valores = [];

    public ConferenciaDosCodigos(Type tipoDoEnum)
    {
        ArgumentNullException.ThrowIfNull(tipoDoEnum);

        _tipo = tipoDoEnum.Name;
        ConferirDescricaoDoEnum(tipoDoEnum);
    }

    /// <summary>Confere o membro e devolve o que ele declarou, ou nada se ele tem qualquer defeito.</summary>
    public CodigoDeclarado? Conferir(FieldInfo campo)
    {
        ArgumentNullException.ThrowIfNull(campo);

        string quem = $"{_tipo}.{campo.Name}";
        int antes = _defeitos.Count;
        ConferirValor(quem, campo);
        CodigoDeclarado? declarado = ConferirAtributo(quem, campo.GetCustomAttribute<CodigoDeContratoAttribute>());

        return _defeitos.Count == antes ? declarado : null;
    }

    /// <summary>Derruba a subida se algum membro teve defeito.</summary>
    /// <exception cref="CodigosDeContratoInvalidosException">O enum é inválido.</exception>
    public void ExigirCoerente()
    {
        if (_defeitos.Count > 0)
        {
            throw new CodigosDeContratoInvalidosException(_defeitos);
        }
    }

    private void ConferirDescricaoDoEnum(Type tipoDoEnum)
    {
        if (tipoDoEnum.GetCustomAttribute<DescricaoDoContratoAttribute>() is { } atributo && string.IsNullOrWhiteSpace(atributo.Descricao))
        {
            _defeitos.Add($"{_tipo}: a descrição do enum foi declarada vazia.");
        }
    }

    private void ConferirValor(string quem, FieldInfo campo)
    {
        object valor = campo.GetRawConstantValue() ?? throw new InvalidOperationException($"{quem}: o membro não tem valor numérico.");

        if (!_valores.TryAdd(valor, quem))
        {
            _defeitos.Add($"{quem}: o valor numérico {valor} já é de {_valores[valor]}.");
        }
    }

    private CodigoDeclarado? ConferirAtributo(string quem, CodigoDeContratoAttribute? atributo)
    {
        if (atributo is null)
        {
            _defeitos.Add($"{quem}: falta [{nameof(CodigoDeContratoAttribute)}].");

            return null;
        }

        ConferirCodigo(quem, atributo.Codigo);
        ConferirRotulo(quem, atributo.Rotulo);
        ConferirDescricao(quem, atributo.Descricao);

        return new CodigoDeclarado(atributo.Codigo, atributo.Rotulo, atributo.Descricao);
    }

    private void ConferirDescricao(string quem, string? descricao)
    {
        if (descricao is not null && string.IsNullOrWhiteSpace(descricao))
        {
            _defeitos.Add($"{quem}: a descrição foi declarada vazia.");
        }
    }

    private void ConferirCodigo(string quem, string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            _defeitos.Add($"{quem}: o código está vazio.");

            if (string.IsNullOrEmpty(codigo))
            {
                return;
            }
        }

        ConferirEspacoDoCodigo(quem, codigo);
        ConferirFormatoDoCodigo(quem, codigo);
        ConferirRepeticaoDoCodigo(quem, codigo);
    }

    private void ConferirFormatoDoCodigo(string quem, string codigo)
    {
        if (codigo.Length > FormatoDoCodigo.ComprimentoMaximo)
        {
            _defeitos.Add($"{quem}: o código deve ter até {FormatoDoCodigo.ComprimentoMaximo} caracteres.");
        }

        if (!FormatoDoCodigo.TemFormatoValido(codigo))
        {
            _defeitos.Add($"{quem}: o código deve começar por letra maiúscula e conter apenas letras maiúsculas, dígitos e sublinhado simples entre partes, sem acento nem hífen.");
        }
    }

    private void ConferirEspacoDoCodigo(string quem, string codigo)
    {
        if (codigo != codigo.Trim())
        {
            _defeitos.Add($"{quem}: o código tem espaço no começo ou no fim.");
        }
    }

    private void ConferirRepeticaoDoCodigo(string quem, string codigo)
    {
        if (!_codigos.TryAdd(codigo, quem))
        {
            _defeitos.Add($"{quem}: o código já é de {_codigos[codigo]}.");
        }
    }

    private void ConferirRotulo(string quem, string? rotulo)
    {
        if (string.IsNullOrWhiteSpace(rotulo))
        {
            _defeitos.Add($"{quem}: o rótulo está vazio.");

            return;
        }

        ConferirEspacoDoRotulo(quem, rotulo);
        ConferirRepeticaoDoRotulo(quem, rotulo);
    }

    private void ConferirEspacoDoRotulo(string quem, string rotulo)
    {
        if (rotulo != rotulo.Trim())
        {
            _defeitos.Add($"{quem}: o rótulo '{rotulo}' tem espaço no começo ou no fim.");
        }
    }

    private void ConferirRepeticaoDoRotulo(string quem, string rotulo)
    {
        if (!_rotulos.TryAdd(rotulo.Trim(), quem))
        {
            _defeitos.Add($"{quem}: o rótulo '{rotulo}' já é de {_rotulos[rotulo.Trim()]}.");
        }
    }
}
