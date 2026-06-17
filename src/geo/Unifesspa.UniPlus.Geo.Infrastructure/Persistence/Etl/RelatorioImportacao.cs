namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

/// <summary>
/// Acumula o resultado de uma carga do ETL DNE por tabela (ADR-0092): contadores
/// (lidos/inseridos/atualizados/órfãos/duplicados/ignorados/degradados) e amostras
/// de divergência (valor cru não-parseável que degradou para <see langword="null"/>,
/// órfão, duplicata) para auditoria. <strong>Reference data público — NUNCA carrega
/// PII</strong>; é emitido ao fim via <c>[LoggerMessage]</c>. A carga não aborta por
/// dado sujo (vai para o relatório); aborta só por falha de infraestrutura.
/// </summary>
internal sealed class RelatorioImportacao
{
    private const int MaxAmostrasPorTabela = 25;

    private readonly Dictionary<string, ContadorTabela> _tabelas = new(StringComparer.Ordinal);

    public RelatorioImportacao(string versaoDataset)
    {
        VersaoDataset = versaoDataset;
    }

    /// <summary>Release DNE (AAAAMM) desta carga.</summary>
    public string VersaoDataset { get; }

    public IReadOnlyDictionary<string, ContadorTabela> Tabelas => _tabelas;

    /// <summary>Obtém (criando se necessário) o contador da tabela informada.</summary>
    public ContadorTabela Tabela(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        if (!_tabelas.TryGetValue(nome, out ContadorTabela? contador))
        {
            contador = new ContadorTabela(nome, MaxAmostrasPorTabela);
            _tabelas[nome] = contador;
        }

        return contador;
    }
}

/// <summary>Contadores e amostras de divergência de uma tabela na carga do ETL.</summary>
internal sealed class ContadorTabela
{
    private readonly int _maxAmostras;
    private readonly List<string> _amostras = [];

    internal ContadorTabela(string tabela, int maxAmostras)
    {
        Tabela = tabela;
        _maxAmostras = maxAmostras;
    }

    public string Tabela { get; }

    public int Lidos { get; private set; }

    public int Inseridos { get; private set; }

    public int Atualizados { get; private set; }

    /// <summary>Registros sem chave natural utilizável (descartados).</summary>
    public int IgnoradosSemChave { get; private set; }

    /// <summary>Registros cuja FK obrigatória não resolveu (descartados).</summary>
    public int Orfaos { get; private set; }

    /// <summary>Chaves naturais repetidas dentro da própria fonte (last-wins).</summary>
    public int Duplicados { get; private set; }

    /// <summary>Valores externos que não parsearam e degradaram para <see langword="null"/>.</summary>
    public int ParsesDegradados { get; private set; }

    /// <summary>Amostras (sem PII) de divergências, limitadas para não inflar o log.</summary>
    public IReadOnlyList<string> Amostras => _amostras;

    public void ContarLido() => Lidos++;

    public void ContarInserido() => Inseridos++;

    public void ContarAtualizado() => Atualizados++;

    /// <summary>Registra um lote de inserções (caminho COPY/merge, onde o split vem de uma contagem agregada, não linha-a-linha).</summary>
    public void ContarInseridos(int quantidade) => Inseridos += quantidade;

    /// <summary>Registra um lote de atualizações (caminho COPY/merge).</summary>
    public void ContarAtualizados(int quantidade) => Atualizados += quantidade;

    /// <summary>Registra um lote de duplicatas intra-fonte removidas (dedup via DISTINCT ON no merge).</summary>
    public void ContarDuplicados(int quantidade) => Duplicados += quantidade;

    public void ContarIgnoradoSemChave() => IgnoradosSemChave++;

    public void ContarOrfao(string amostra)
    {
        Orfaos++;
        Amostrar(amostra);
    }

    public void ContarDuplicado(string amostra)
    {
        Duplicados++;
        Amostrar(amostra);
    }

    public void ContarParseDegradado(string amostra)
    {
        ParsesDegradados++;
        Amostrar(amostra);
    }

    private void Amostrar(string amostra)
    {
        if (_amostras.Count < _maxAmostras)
        {
            _amostras.Add(amostra);
        }
    }
}
