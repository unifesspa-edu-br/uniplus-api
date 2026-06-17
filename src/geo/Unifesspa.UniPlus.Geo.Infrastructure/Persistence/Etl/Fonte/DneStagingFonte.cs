namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Npgsql;

/// <summary>
/// <see cref="IGeoFonteDados"/> que lê o dataset DNE de um <strong>schema de
/// staging</strong> (os 15 dumps Navicat restaurados via <c>psql</c>), por
/// <c>SELECT</c> streamado (<see cref="DbDataReader"/> — leitura linha-a-linha, uso
/// de memória estável mesmo nas tabelas grandes). O nome de cada tabela é derivado
/// da <see cref="Versao"/> (<c>tbl_cep_{versao}_n_{sufixo}</c>), garantindo que a
/// release lida é exatamente a que o importador carimba como proveniência.
/// </summary>
/// <remarks>
/// <see cref="Versao"/> e o nome do schema são validados no construtor (não há como
/// parametrizar identificadores de tabela em SQL): só dígitos (AAAAMM) e identificador
/// Postgres seguro são aceitos — defesa contra injeção via configuração administrativa.
/// </remarks>
internal sealed class DneStagingFonte : IGeoFonteDados
{
    public const string SchemaPadrao = "dne_staging";

    private readonly string _connectionString;
    private readonly string _schema;

    public DneStagingFonte(string connectionString, string versao, string schema = SchemaPadrao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(versao);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        if (!VersaoValida(versao))
        {
            throw new ArgumentException(
                $"Versão do dataset inválida: '{versao}'. Esperado AAAAMM (6 dígitos).",
                nameof(versao));
        }

        if (!IdentificadorValido(schema))
        {
            throw new ArgumentException(
                $"Nome de schema inválido: '{schema}'.",
                nameof(schema));
        }

        _connectionString = connectionString;
        _schema = schema;
        Versao = versao;
    }

    public string Versao { get; }

    public IAsyncEnumerable<PaisCru> LerPaisesAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"SELECT sigla_iso, sigla, nome_pt, pais_bcb, pais_rbf, pais_sped, pais_siscomex FROM {Tabela("paises_ibge")}",
            static r => new PaisCru(Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6)),
            cancellationToken);

    public IAsyncEnumerable<EstadoCru> LerEstadosAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"SELECT sigla, estado, estado_sem_acento, regiao, capital, faixa_ini, faixa_fim, latitude, longitude FROM {Tabela("estado")}",
            static r => new EstadoCru(Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6), Texto(r, 7), Texto(r, 8)),
            cancellationToken);

    public IAsyncEnumerable<EstadoIndicadorCru> LerEstadoIndicadoresAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"""
             SELECT uf, codigo_ibge, gentilico, governador, area_territorial_km2,
                    populacao_residente_2022, densidade_demografica_hab_km2,
                    matriculas_ensino_fundamental_2023, idh_indice_desenv_humano,
                    total_receitas_brutas_realizadas, total_despesas_brutas_empenhadas,
                    rendimento_mensal_per_capita, total_veiculos_2023
             FROM {Tabela("estado_ibge")}
             """,
            static r => new EstadoIndicadorCru(
                Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6),
                Texto(r, 7), Texto(r, 8), Texto(r, 9), Texto(r, 10), Texto(r, 11), Texto(r, 12)),
            cancellationToken);

    public IAsyncEnumerable<EstadoFaixaCru> LerEstadoFaixasAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"SELECT sigla, regiao, faixa_ini, faixa_fim FROM {Tabela("estado_faixa")}",
            static r => new EstadoFaixaCru(Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3)),
            cancellationToken);

    public IAsyncEnumerable<CidadeCru> LerCidadesAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"SELECT cidade_ibge, cidade, cidade_sem_acento, estado, ddd, latitude, longitude FROM {Tabela("cidade")}",
            static r => new CidadeCru(Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6)),
            cancellationToken);

    public IAsyncEnumerable<CidadeTerritorioCru> LerCidadeTerritoriosAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"""
             SELECT cidade_ibge, mesorregiao, mesorregiao_nome, microrregiao, microrregiao_nome,
                    regiao_intermediaria, regiao_intermediaria_nome,
                    regiao_geografica_imediata, regiao_geografica_imediata_nome
             FROM {Tabela("cidade_ibge_territorio")}
             """,
            static r => new CidadeTerritorioCru(
                Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4),
                Texto(r, 5), Texto(r, 6), Texto(r, 7), Texto(r, 8)),
            cancellationToken);

    public IAsyncEnumerable<CidadeIndicadorCru> LerCidadeIndicadoresAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"""
             SELECT cidade_ibge, gentilico, prefeito, area_territorial_km2, populacao_residente,
                    densidade_demografica, escolarizacao_6_a_14_anos, idice_de_desenv_humano,
                    mortalidade_infantil, receitas_realizadas, despesas_empenhadas,
                    pib_per_capita, aniversario_municipio
             FROM {Tabela("cidade_ibge")}
             """,
            static r => new CidadeIndicadorCru(
                Texto(r, 0), Texto(r, 1), Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6),
                Texto(r, 7), Texto(r, 8), Texto(r, 9), Texto(r, 10), Texto(r, 11), Texto(r, 12)),
            cancellationToken);

    public IAsyncEnumerable<CidadeFaixaCru> LerCidadeFaixasAsync(CancellationToken cancellationToken) =>
        ConsultarAsync(
            $"SELECT cidade_ibge, faixa_ini, faixa_fim FROM {Tabela("cidade_faixa")}",
            static r => new CidadeFaixaCru(Texto(r, 0), Texto(r, 1), Texto(r, 2)),
            cancellationToken);

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "O SQL é literal de código (lista de colunas fixa) somada ao nome de tabela cujo schema e versão são validados no construtor (dígitos/identificador Postgres seguro). Não há entrada de usuário na query — não é parametrizável por se tratar de identificador de tabela.")]
    private async IAsyncEnumerable<T> ConsultarAsync<T>(
        string sql,
        Func<DbDataReader, T> mapear,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        NpgsqlConnection conexao = new(_connectionString);
        await using (conexao.ConfigureAwait(false))
        {
            await conexao.OpenAsync(cancellationToken).ConfigureAwait(false);

            NpgsqlCommand comando = conexao.CreateCommand();
            await using (comando.ConfigureAwait(false))
            {
                comando.CommandText = sql;
                DbDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (leitor.ConfigureAwait(false))
                {
                    while (await leitor.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        yield return mapear(leitor);
                    }
                }
            }
        }
    }

    // Identificador de tabela qualificado e citado. Schema e versão já validados —
    // o sufixo é literal de código; não há entrada de usuário no nome da tabela.
    private string Tabela(string sufixo) => $"\"{_schema}\".\"tbl_cep_{Versao}_n_{sufixo}\"";

    // As colunas projetadas pela fonte são todas text/varchar na DNE — GetString evita
    // o boxing de GetValue().ToString() (relevante no volume dos logradouros, #673).
    private static string? Texto(DbDataReader leitor, int ordinal) =>
        leitor.IsDBNull(ordinal) ? null : leitor.GetString(ordinal);

    private static bool VersaoValida(string versao) =>
        versao.Length == 6 && versao.All(char.IsAsciiDigit);

    private static bool IdentificadorValido(string identificador) =>
        // PostgreSQL trunca identificadores em 63 bytes — defesa em profundidade.
        identificador.Length is >= 1 and <= 63
        && (char.IsAsciiLetter(identificador[0]) || identificador[0] == '_')
        && identificador.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
