namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Reflection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Decisao;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A conferência de um enum de permissões: cada membro, na ordem em que o enum o declara, é conferido quanto ao atributo,
/// aos textos, ao código, ao escopo e à natureza, e cada defeito é acumulado com o membro responsável (ADR-0151).
/// </summary>
internal sealed class ConferenciaDoEnum
{
    private readonly string _tipo;
    private readonly string? _modulo;
    private readonly List<string> _problemas = [];
    private readonly Dictionary<string, string> _codigos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _nomes = new(StringComparer.OrdinalIgnoreCase);

    public ConferenciaDoEnum(Type tipoDoEnum, string? modulo)
    {
        ArgumentNullException.ThrowIfNull(tipoDoEnum);

        _tipo = tipoDoEnum.Name;
        _modulo = modulo;
        if (string.IsNullOrWhiteSpace(modulo))
        {
            _problemas.Add($"{_tipo}: falta [{nameof(ModuloDePermissoesAttribute)}] com o módulo do enum.");
        }
    }

    /// <summary>Confere o membro e devolve a definição, ou nada se ele tem qualquer problema.</summary>
    public DefinicaoDePermissao? Conferir(FieldInfo campo)
    {
        ArgumentNullException.ThrowIfNull(campo);

        string quem = $"{_tipo}.{campo.Name}";

        return campo.GetCustomAttribute<PermissaoAttribute>() is { } atributo
            ? ConferirMembro(quem, atributo)
            : SemAtributo(quem);
    }

    /// <summary>Acrescenta os pares repetidos entre as definições válidas e derruba a subida se houver qualquer problema.</summary>
    /// <exception cref="CatalogoDePermissoesInvalidoException">O enum é inválido.</exception>
    public void ExigirCoerente(IEnumerable<DefinicaoDePermissao> definicoes)
    {
        _problemas.AddRange(UnicidadeDoPar.Problemas(definicoes.Select(static d => (d.Codigo, d))));

        if (_problemas.Count > 0)
        {
            throw new CatalogoDePermissoesInvalidoException(_problemas);
        }
    }

    private DefinicaoDePermissao? SemAtributo(string quem)
    {
        _problemas.Add($"{quem}: falta [{nameof(PermissaoAttribute)}].");

        return null;
    }

    private DefinicaoDePermissao? ConferirMembro(string quem, PermissaoAttribute atributo)
    {
        int antes = _problemas.Count;
        CodigoDePermissao? codigo = ConferirAtributo(quem, atributo);
        Result<PermissionRequirement> requisito = ConferirRequisito(quem, atributo);

        return _problemas.Count == antes && codigo is not null
            ? DefinicaoDePermissao.Declarada(atributo, codigo, requisito.Value!)
            : null;
    }

    private CodigoDePermissao? ConferirAtributo(string quem, PermissaoAttribute atributo)
    {
        ConferirTextos(quem, atributo);
        CodigoDePermissao? codigo = ConferirCodigo(quem, atributo.Codigo);
        ConferirEscopo(quem, atributo);
        ConferirNatureza(quem, atributo);

        return codigo;
    }

    private Result<PermissionRequirement> ConferirRequisito(string quem, PermissaoAttribute atributo)
    {
        Result<PermissionRequirement> requisito = atributo.Requisito();
        if (requisito.IsFailure)
        {
            _problemas.Add($"{quem}: metadados recusados — {requisito.Error!.Message}");
        }

        return requisito;
    }

    private void ConferirTextos(string quem, PermissaoAttribute atributo)
    {
        if (string.IsNullOrWhiteSpace(atributo.Nome))
        {
            _problemas.Add($"{quem}: o nome está vazio.");
        }
        else if (!_nomes.TryAdd(atributo.Nome.Trim(), quem))
        {
            _problemas.Add($"{quem}: o nome '{atributo.Nome}' já é de {_nomes[atributo.Nome.Trim()]}.");
        }

        if (string.IsNullOrWhiteSpace(atributo.Descricao))
        {
            _problemas.Add($"{quem}: a descrição está vazia.");
        }
    }

    private CodigoDePermissao? ConferirCodigo(string quem, string? texto)
    {
        if (!CodigoDePermissao.TentarCriar(texto, out CodigoDePermissao? codigo))
        {
            _problemas.Add($"{quem}: o código '{texto}' não segue {{módulo}}:{{resource}}:{{ação}} em minúsculas, dígitos e hífen.");
            return null;
        }

        ConferirRepeticaoDoCodigo(quem, codigo);
        ConferirPrefixoDoModulo(quem, codigo);

        return codigo;
    }

    private void ConferirRepeticaoDoCodigo(string quem, CodigoDePermissao codigo)
    {
        if (!_codigos.TryAdd(codigo.Valor, quem))
        {
            _problemas.Add($"{quem}: o código '{codigo}' já é de {_codigos[codigo.Valor]}.");
        }
    }

    private void ConferirPrefixoDoModulo(string quem, CodigoDePermissao codigo)
    {
        if (!string.IsNullOrWhiteSpace(_modulo) && codigo.Modulo != _modulo)
        {
            _problemas.Add($"{quem}: o código '{codigo}' tem o prefixo '{codigo.Modulo}', mas o módulo do enum é '{_modulo}'.");
        }
    }

    private void ConferirEscopo(string quem, PermissaoAttribute atributo)
    {
        foreach (string campo in (atributo.EscopoContextoObrigatorio ?? [])
            .Where(static campo => !CamposDeContexto.NomesConhecidos.Contains(campo)))
        {
            _problemas.Add($"{quem}: o campo de contexto '{campo}' não é verificável pela decisão.");
        }
    }

    private void ConferirNatureza(string quem, PermissaoAttribute atributo)
    {
        if (!Enum.IsDefined(atributo.Natureza))
        {
            _problemas.Add($"{quem}: a natureza '{atributo.Natureza}' não é dado nem arquivo.");
        }
    }
}
