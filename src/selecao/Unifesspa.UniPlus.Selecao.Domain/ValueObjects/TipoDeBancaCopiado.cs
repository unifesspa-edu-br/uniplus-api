namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// O que a banca guarda do tipo de banca de que foi criada: o tipo, o nome do cadastro, o papel, as duas marcas e os tipos de
/// decisão que o tipo julga. É copiado do cadastro quando a banca nasce ou troca de tipo e se mantém enquanto o tipo dela não
/// muda; cada banca é julgada pela própria cópia, e nada é lido do cadastro depois.
/// </summary>
public sealed record TipoDeBancaCopiado
{
    private readonly List<TipoMotivoDeDecisao> _tiposDeDecisaoJulgados = [];

    // Leitura pelo EF Core, que preenche as propriedades e o campo da lista.
    private TipoDeBancaCopiado()
    {
    }

    /// <exception cref="ArgumentException">O id de origem ou o nome faltam.</exception>
    public TipoDeBancaCopiado(
        Guid origemId,
        TipoDeBanca codigo,
        string nome,
        PapelDaBanca papel,
        bool exigeDuasOuMaisBancas,
        bool encerraRecursoNaPrimeiraInstancia,
        IEnumerable<TipoMotivoDeDecisao> tiposDeDecisaoJulgados)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentNullException.ThrowIfNull(tiposDeDecisaoJulgados);
        if (origemId == Guid.Empty)
        {
            throw new ArgumentException("O id de origem do tipo de banca é obrigatório.", nameof(origemId));
        }

        OrigemId = origemId;
        Codigo = codigo;
        Nome = nome.Trim();
        Papel = papel;
        ExigeDuasOuMaisBancas = exigeDuasOuMaisBancas;
        EncerraRecursoNaPrimeiraInstancia = encerraRecursoNaPrimeiraInstancia;
        _tiposDeDecisaoJulgados.AddRange(tiposDeDecisaoJulgados.Distinct().OrderBy(static tipo => tipo.Codigo(), StringComparer.Ordinal));
    }

    /// <summary>O id do tipo de banca no cadastro, no momento da cópia.</summary>
    public Guid OrigemId { get; }

    /// <summary>O tipo do vocabulário, que é a chave natural da banca.</summary>
    public TipoDeBanca Codigo { get; }

    /// <summary>O nome do tipo no cadastro, que o cliente exibe como veio; as mensagens usam o rótulo do tipo.</summary>
    public string Nome { get; } = string.Empty;

    /// <summary>O que as bancas do tipo fazem no processo.</summary>
    public PapelDaBanca Papel { get; }

    /// <summary>O tipo só vale com duas ou mais bancas no mesmo lugar.</summary>
    public bool ExigeDuasOuMaisBancas { get; }

    /// <summary>O recurso do que o tipo avalia termina na primeira instância.</summary>
    public bool EncerraRecursoNaPrimeiraInstancia { get; }

    /// <summary>Os tipos de decisão que o tipo julga, em ordem alfabética ordinal do código e sem repetição.</summary>
    public IReadOnlyList<TipoMotivoDeDecisao> TiposDeDecisaoJulgados => _tiposDeDecisaoJulgados.AsReadOnly();

    /// <summary>Se a banca atribui a nota ou o parecer do que avalia.</summary>
    public bool AvaliaOMerito => Papel == PapelDaBanca.AvaliaOMerito;

    /// <summary>Se o papel da banca julga recurso.</summary>
    public bool JulgaRecurso => PapeisQueJulgamRecurso.Contem(Papel);

    /// <inheritdoc />
    public bool Equals(TipoDeBancaCopiado? outro) =>
        outro is not null
        && OrigemId == outro.OrigemId
        && Codigo == outro.Codigo
        && string.Equals(Nome, outro.Nome, StringComparison.Ordinal)
        && Papel == outro.Papel
        && ExigeDuasOuMaisBancas == outro.ExigeDuasOuMaisBancas
        && EncerraRecursoNaPrimeiraInstancia == outro.EncerraRecursoNaPrimeiraInstancia
        && _tiposDeDecisaoJulgados.SequenceEqual(outro._tiposDeDecisaoJulgados);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(OrigemId);
        hash.Add(Codigo);
        hash.Add(Nome, StringComparer.Ordinal);
        hash.Add(Papel);
        hash.Add(ExigeDuasOuMaisBancas);
        hash.Add(EncerraRecursoNaPrimeiraInstancia);
        _tiposDeDecisaoJulgados.ForEach(tipo => hash.Add(tipo));
        return hash.ToHashCode();
    }
}
