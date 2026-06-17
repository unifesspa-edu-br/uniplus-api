namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Importa o topo da hierarquia do Geo — País (só Brasil), Estado (+indicador
/// +faixas) e Cidade (+territorial embutido, +indicador, +faixas) — a partir do
/// dataset DNE (ADR-0092). Toda a carga roda numa <strong>única transação</strong>:
/// <c>SaveChanges</c> por nível, mas commit só no fim — uma falha no meio faz
/// rollback total, evitando publicar uma release mista. O upsert por chave natural
/// (<c>Importar</c>/<c>Atualizar</c> do domínio) torna a recarga idempotente.
/// </summary>
/// <remarks>
/// Esta Story (#672) faz <strong>carga inicial + upsert</strong>. A política de
/// <em>stale</em> (marcar <c>vigente=false</c> para chaves ausentes numa nova
/// release) é da Story de atualização periódica (#674) — aqui todo registro tocado
/// nasce/permanece vigente.
/// </remarks>
internal sealed partial class GeoImportadorPaisEstadoCidade : IGeoImportador
{
    private readonly GeoDbContext _contexto;
    private readonly ILogger<GeoImportadorPaisEstadoCidade> _logger;

    public GeoImportadorPaisEstadoCidade(GeoDbContext contexto, ILogger<GeoImportadorPaisEstadoCidade> logger)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(logger);

        _contexto = contexto;
        _logger = logger;
    }

    public async Task<RelatorioImportacao> ImportarAsync(IGeoFonteDados fonte, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fonte);

        RelatorioImportacao relatorio = new(fonte.Versao);
        LogCargaIniciada(_logger, fonte.Versao);

        IDbContextTransaction transacao =
            await _contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transacao.ConfigureAwait(false))
        {
            Guid brasilId = await ImportarPaisAsync(fonte, relatorio, cancellationToken).ConfigureAwait(false);
            if (brasilId == Guid.Empty)
            {
                // País-âncora (Brasil) ausente no dataset: sem ele estados/cidades
                // ficariam órfãos e a carga retornaria verde-vazia, indistinguível de
                // uma base legitimamente vazia. Sinaliza explicitamente o caso.
                LogBrasilAusente(_logger, fonte.Versao);
            }

            // Agrega os indicadores contando lidos/sem-chave/duplicados no próprio
            // contador da tabela — o relatório reflete o total da fonte, não só os
            // registros com chave (a agregação não pode descartar em silêncio).
            Dictionary<string, EstadoIndicadorCru> indicadoresEstado =
                await AgregarContandoAsync(fonte.LerEstadoIndicadoresAsync(cancellationToken), i => ChaveSigla(i.Uf), relatorio.Tabela("estado_indicador")).ConfigureAwait(false);

            Dictionary<string, Guid> estadosPorUf =
                await ImportarEstadosAsync(fonte, brasilId, indicadoresEstado, relatorio, cancellationToken).ConfigureAwait(false);
            await ImportarEstadoIndicadoresAsync(estadosPorUf, indicadoresEstado, fonte.Versao, relatorio, cancellationToken).ConfigureAwait(false);
            await ImportarEstadoFaixasAsync(fonte, estadosPorUf, fonte.Versao, relatorio, cancellationToken).ConfigureAwait(false);

            Dictionary<string, CidadeTerritorioCru> territorios =
                await AgregarPorChaveAsync(fonte.LerCidadeTerritoriosAsync(cancellationToken), t => ChaveCodigo(t.CodigoIbge)).ConfigureAwait(false);

            Dictionary<string, Guid> cidadesPorCodigo =
                await ImportarCidadesAsync(fonte, estadosPorUf, territorios, relatorio, cancellationToken).ConfigureAwait(false);

            Dictionary<string, CidadeIndicadorCru> indicadoresCidade =
                await AgregarContandoAsync(fonte.LerCidadeIndicadoresAsync(cancellationToken), i => ChaveCodigo(i.CodigoIbge), relatorio.Tabela("cidade_indicador")).ConfigureAwait(false);
            await ImportarCidadeIndicadoresAsync(cidadesPorCodigo, indicadoresCidade, fonte.Versao, relatorio, cancellationToken).ConfigureAwait(false);
            await ImportarCidadeFaixasAsync(fonte, cidadesPorCodigo, fonte.Versao, relatorio, cancellationToken).ConfigureAwait(false);

            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        int inseridos = Total(relatorio, t => t.Inseridos);
        int atualizados = Total(relatorio, t => t.Atualizados);
        int orfaos = Total(relatorio, t => t.Orfaos);
        int degradados = Total(relatorio, t => t.ParsesDegradados);
        LogCargaConcluida(_logger, fonte.Versao, inseridos, atualizados, orfaos, degradados);

        return relatorio;
    }

    private async Task<Guid> ImportarPaisAsync(IGeoFonteDados fonte, RelatorioImportacao relatorio, CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("pais");
        Dictionary<string, Pais> existentes =
            await _contexto.Paises.ToDictionaryAsync(p => p.SiglaIso, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        Dictionary<string, Pais> jaVistos = new(StringComparer.Ordinal);
        Guid brasilId = Guid.Empty;

        await foreach (PaisCru cru in fonte.LerPaisesAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? siglaIso = ChaveSigla(cru.SiglaIso);
            if (siglaIso is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            // Carga restrita ao Brasil (decisão TL do Epic) — os demais países não
            // são erro, apenas fora de escopo; não contam como ignorados.
            if (!string.Equals(siglaIso, "BRA", StringComparison.Ordinal))
            {
                continue;
            }

            Pais? pais = Upsert(
                siglaIso,
                jaVistos,
                existentes,
                () => Pais.Importar(siglaIso, cru.Sigla ?? string.Empty, cru.Nome ?? string.Empty, cru.CodigoBcb, cru.CodigoRfb, cru.CodigoSped, cru.CodigoSiscomex, fonte.Versao),
                existente => existente.Atualizar(cru.Sigla ?? string.Empty, cru.Nome ?? string.Empty, cru.CodigoBcb, cru.CodigoRfb, cru.CodigoSped, cru.CodigoSiscomex, fonte.Versao),
                _contexto.Paises,
                contador);

            if (pais is not null)
            {
                brasilId = pais.Id;
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "pais", contador.Lidos, contador.Inseridos, contador.Atualizados);
        return brasilId;
    }

    private async Task<Dictionary<string, Guid>> ImportarEstadosAsync(
        IGeoFonteDados fonte,
        Guid brasilId,
        Dictionary<string, EstadoIndicadorCru> indicadores,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("estado");
        Dictionary<string, Guid> estadosPorUf = new(StringComparer.Ordinal);

        // Sem o País (Brasil) carregado, os estados ficariam órfãos de FK — aborta o nível.
        if (brasilId == Guid.Empty)
        {
            return estadosPorUf;
        }

        Dictionary<string, Estado> existentes =
            await _contexto.Estados.ToDictionaryAsync(e => e.Uf, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        Dictionary<string, Estado> jaVistos = new(StringComparer.Ordinal);

        await foreach (EstadoCru cru in fonte.LerEstadosAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? uf = ChaveSigla(cru.Uf);
            if (uf is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            indicadores.TryGetValue(uf, out EstadoIndicadorCru? indicador);
            string? codigoIbge = indicador?.CodigoIbge;
            decimal? latitude = ParseTolerante.ParaDecimal(cru.Latitude);
            decimal? longitude = ParseTolerante.ParaDecimal(cru.Longitude);
            Point? coordenada = PontoFactory.Criar(cru.Latitude, cru.Longitude);

            Estado? estado = Upsert(
                uf,
                jaVistos,
                existentes,
                () => Estado.Importar(brasilId, uf, cru.Nome ?? string.Empty, cru.NomeSemAcento, cru.Regiao, cru.Capital, codigoIbge, latitude, longitude, coordenada, cru.FaixaIni, cru.FaixaFim, fonte.Versao),
                existente => existente.Atualizar(brasilId, cru.Nome ?? string.Empty, cru.NomeSemAcento, cru.Regiao, cru.Capital, codigoIbge, latitude, longitude, coordenada, cru.FaixaIni, cru.FaixaFim, fonte.Versao),
                _contexto.Estados,
                contador);

            if (estado is not null)
            {
                estadosPorUf[uf] = estado.Id;
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "estado", contador.Lidos, contador.Inseridos, contador.Atualizados);
        return estadosPorUf;
    }

    private async Task ImportarEstadoIndicadoresAsync(
        Dictionary<string, Guid> estadosPorUf,
        Dictionary<string, EstadoIndicadorCru> indicadores,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("estado_indicador");
        Dictionary<Guid, EstadoIndicador> existentes =
            await _contexto.EstadoIndicadores.ToDictionaryAsync(i => i.EstadoId, cancellationToken).ConfigureAwait(false);

        // Lidos/sem-chave/duplicados já foram contados na agregação (AgregarContandoAsync);
        // aqui só resta resolver órfão (UF sem estado carregado) e o upsert.
        foreach ((string uf, EstadoIndicadorCru cru) in indicadores)
        {
            if (!estadosPorUf.TryGetValue(uf, out Guid estadoId))
            {
                contador.ContarOrfao(uf);
                continue;
            }

            decimal? area = MetricaDecimal(cru.AreaTerritorialKm2, "area_territorial_km2", contador);
            int? populacao = MetricaInteira(cru.PopulacaoResidente2022, "populacao_residente_2022", contador);
            decimal? densidade = MetricaDecimal(cru.DensidadeDemografica, "densidade_demografica_hab_km2", contador);
            int? matriculas = MetricaInteira(cru.MatriculasEnsinoFundamental2023, "matriculas_ensino_fundamental_2023", contador);
            decimal? idh = MetricaDecimal(cru.Idh, "idh_indice_desenv_humano", contador);
            decimal? receitas = MetricaDecimal(cru.ReceitasBrutas, "total_receitas_brutas_realizadas", contador);
            decimal? despesas = MetricaDecimal(cru.DespesasBrutas, "total_despesas_brutas_empenhadas", contador);
            int? rendimento = MetricaInteira(cru.RendimentoMensalPerCapita, "rendimento_mensal_per_capita", contador);
            int? veiculos = MetricaInteira(cru.TotalVeiculos2023, "total_veiculos_2023", contador);

            if (existentes.TryGetValue(estadoId, out EstadoIndicador? existente))
            {
                existente.Atualizar(cru.Gentilico, cru.Governador, area, populacao, densidade, matriculas, idh, receitas, despesas, rendimento, veiculos, versao);
                contador.ContarAtualizado();
            }
            else
            {
                Result<EstadoIndicador> criado = EstadoIndicador.Importar(estadoId, cru.Gentilico, cru.Governador, area, populacao, densidade, matriculas, idh, receitas, despesas, rendimento, veiculos, versao);
                if (criado.IsFailure)
                {
                    contador.ContarIgnoradoSemChave();
                    continue;
                }

                _contexto.EstadoIndicadores.Add(criado.Value!);
                contador.ContarInserido();
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "estado_indicador", contador.Lidos, contador.Inseridos, contador.Atualizados);
    }

    private async Task ImportarEstadoFaixasAsync(
        IGeoFonteDados fonte,
        Dictionary<string, Guid> estadosPorUf,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("estado_faixa");

        // Upsert por chave natural (estado_id, cep_inicial, cep_final) — UNIQUE — em
        // vez de delete+insert: preserva o Id da faixa entre cargas (idempotência real)
        // e o dedup intra-fonte do Upsert evita que faixas repetidas na fonte estourem
        // a UNIQUE e provoquem rollback total. A remoção de faixas que somem entre
        // releases (stale) é da Story de atualização periódica (#674).
        Guid[] estadoIds = [.. estadosPorUf.Values];
        Dictionary<string, EstadoFaixaCep> existentes =
            (await _contexto.EstadoFaixasCep.Where(f => estadoIds.Contains(f.EstadoId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(f => ChaveFaixa(f.EstadoId, f.CepInicial, f.CepFinal), StringComparer.Ordinal);
        Dictionary<string, EstadoFaixaCep> jaVistas = new(StringComparer.Ordinal);

        await foreach (EstadoFaixaCru cru in fonte.LerEstadoFaixasAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? uf = ChaveSigla(cru.Uf);
            if (uf is null || !estadosPorUf.TryGetValue(uf, out Guid estadoId))
            {
                contador.ContarOrfao(uf ?? "(sem uf)");
                continue;
            }

            string? cepInicial = ChaveCodigo(cru.FaixaIni);
            string? cepFinal = ChaveCodigo(cru.FaixaFim);
            if (cepInicial is null || cepFinal is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            string chave = ChaveFaixa(estadoId, cepInicial, cepFinal);
            Upsert(
                chave,
                jaVistas,
                existentes,
                () => EstadoFaixaCep.Importar(estadoId, cepInicial, cepFinal, cru.Descricao, versao),
                existente => existente.Atualizar(cru.Descricao, versao),
                _contexto.EstadoFaixasCep,
                contador);
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "estado_faixa", contador.Lidos, contador.Inseridos, contador.Atualizados);
    }

    private async Task<Dictionary<string, Guid>> ImportarCidadesAsync(
        IGeoFonteDados fonte,
        Dictionary<string, Guid> estadosPorUf,
        Dictionary<string, CidadeTerritorioCru> territorios,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("cidade");
        Dictionary<string, Guid> cidadesPorCodigo = new(StringComparer.Ordinal);

        Dictionary<string, Cidade> existentes =
            await _contexto.Cidades.ToDictionaryAsync(c => c.CodigoIbge, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
        Dictionary<string, Cidade> jaVistas = new(StringComparer.Ordinal);

        await foreach (CidadeCru cru in fonte.LerCidadesAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? codigoIbge = ChaveCodigo(cru.CodigoIbge);
            if (codigoIbge is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            string? uf = ChaveSigla(cru.Uf);
            if (uf is null || !estadosPorUf.TryGetValue(uf, out Guid estadoId))
            {
                contador.ContarOrfao(codigoIbge);
                continue;
            }

            territorios.TryGetValue(codigoIbge, out CidadeTerritorioCru? territorio);
            decimal? latitude = ParseTolerante.ParaDecimal(cru.Latitude);
            decimal? longitude = ParseTolerante.ParaDecimal(cru.Longitude);
            Point? coordenada = PontoFactory.Criar(cru.Latitude, cru.Longitude);

            Cidade? cidade = Upsert(
                codigoIbge,
                jaVistas,
                existentes,
                () => Cidade.Importar(
                    estadoId, uf, codigoIbge, cru.Nome ?? string.Empty, cru.NomeSemAcento, cru.Ddd, latitude, longitude, coordenada,
                    territorio?.MesorregiaoCodigo, territorio?.MesorregiaoNome, territorio?.MicrorregiaoCodigo, territorio?.MicrorregiaoNome,
                    territorio?.RegiaoIntermediariaCodigo, territorio?.RegiaoIntermediariaNome, territorio?.RegiaoImediataCodigo, territorio?.RegiaoImediataNome,
                    fonte.Versao),
                existente => existente.Atualizar(
                    estadoId, uf, cru.Nome ?? string.Empty, cru.NomeSemAcento, cru.Ddd, latitude, longitude, coordenada,
                    territorio?.MesorregiaoCodigo, territorio?.MesorregiaoNome, territorio?.MicrorregiaoCodigo, territorio?.MicrorregiaoNome,
                    territorio?.RegiaoIntermediariaCodigo, territorio?.RegiaoIntermediariaNome, territorio?.RegiaoImediataCodigo, territorio?.RegiaoImediataNome,
                    fonte.Versao),
                _contexto.Cidades,
                contador);

            if (cidade is not null)
            {
                cidadesPorCodigo[codigoIbge] = cidade.Id;
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "cidade", contador.Lidos, contador.Inseridos, contador.Atualizados);
        return cidadesPorCodigo;
    }

    private async Task ImportarCidadeIndicadoresAsync(
        Dictionary<string, Guid> cidadesPorCodigo,
        Dictionary<string, CidadeIndicadorCru> indicadores,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("cidade_indicador");
        Dictionary<Guid, CidadeIndicador> existentes =
            await _contexto.CidadeIndicadores.ToDictionaryAsync(i => i.CidadeId, cancellationToken).ConfigureAwait(false);

        // Lidos/sem-chave/duplicados contados na agregação; aqui só órfão + upsert.
        foreach ((string codigo, CidadeIndicadorCru cru) in indicadores)
        {
            if (!cidadesPorCodigo.TryGetValue(codigo, out Guid cidadeId))
            {
                contador.ContarOrfao(codigo);
                continue;
            }

            decimal? area = MetricaDecimal(cru.AreaTerritorialKm2, "area_territorial_km2", contador);
            int? populacao = MetricaInteira(cru.PopulacaoResidente, "populacao_residente", contador);
            decimal? densidade = MetricaDecimal(cru.DensidadeDemografica, "densidade_demografica", contador);
            decimal? escolarizacao = MetricaDecimal(cru.Escolarizacao6a14, "escolarizacao_6_a_14_anos", contador);
            decimal? idh = MetricaDecimal(cru.Idh, "idice_de_desenv_humano", contador);
            decimal? mortalidade = MetricaDecimal(cru.MortalidadeInfantil, "mortalidade_infantil", contador);
            decimal? receitas = MetricaDecimal(cru.Receitas, "receitas_realizadas", contador);
            decimal? despesas = MetricaDecimal(cru.Despesas, "despesas_empenhadas", contador);
            decimal? pib = MetricaDecimal(cru.PibPerCapita, "pib_per_capita", contador);

            if (existentes.TryGetValue(cidadeId, out CidadeIndicador? existente))
            {
                existente.Atualizar(cru.Gentilico, cru.Prefeito, area, populacao, densidade, escolarizacao, idh, mortalidade, receitas, despesas, pib, cru.Aniversario, versao);
                contador.ContarAtualizado();
            }
            else
            {
                Result<CidadeIndicador> criado = CidadeIndicador.Importar(cidadeId, cru.Gentilico, cru.Prefeito, area, populacao, densidade, escolarizacao, idh, mortalidade, receitas, despesas, pib, cru.Aniversario, versao);
                if (criado.IsFailure)
                {
                    contador.ContarIgnoradoSemChave();
                    continue;
                }

                _contexto.CidadeIndicadores.Add(criado.Value!);
                contador.ContarInserido();
            }
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "cidade_indicador", contador.Lidos, contador.Inseridos, contador.Atualizados);
    }

    private async Task ImportarCidadeFaixasAsync(
        IGeoFonteDados fonte,
        Dictionary<string, Guid> cidadesPorCodigo,
        string versao,
        RelatorioImportacao relatorio,
        CancellationToken cancellationToken)
    {
        ContadorTabela contador = relatorio.Tabela("cidade_faixa");

        // Upsert por chave natural (cidade_id, cep_inicial, cep_final) — UNIQUE — com
        // dedup intra-fonte (ver estado_faixa): preserva Id e não estoura a UNIQUE.
        Guid[] cidadeIds = [.. cidadesPorCodigo.Values];
        Dictionary<string, CidadeFaixaCep> existentes =
            (await _contexto.CidadeFaixasCep.Where(f => cidadeIds.Contains(f.CidadeId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(f => ChaveFaixa(f.CidadeId, f.CepInicial, f.CepFinal), StringComparer.Ordinal);
        Dictionary<string, CidadeFaixaCep> jaVistas = new(StringComparer.Ordinal);

        await foreach (CidadeFaixaCru cru in fonte.LerCidadeFaixasAsync(cancellationToken).ConfigureAwait(false))
        {
            contador.ContarLido();
            string? codigo = ChaveCodigo(cru.CodigoIbge);
            if (codigo is null || !cidadesPorCodigo.TryGetValue(codigo, out Guid cidadeId))
            {
                contador.ContarOrfao(codigo ?? "(sem codigo)");
                continue;
            }

            string? cepInicial = ChaveCodigo(cru.FaixaIni);
            string? cepFinal = ChaveCodigo(cru.FaixaFim);
            if (cepInicial is null || cepFinal is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            string chave = ChaveFaixa(cidadeId, cepInicial, cepFinal);
            Upsert(
                chave,
                jaVistas,
                existentes,
                () => CidadeFaixaCep.Importar(cidadeId, cepInicial, cepFinal, versao),
                existente => existente.Atualizar(versao),
                _contexto.CidadeFaixasCep,
                contador);
        }

        await _contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogTabelaConcluida(_logger, "cidade_faixa", contador.Lidos, contador.Inseridos, contador.Atualizados);
    }

    // Upsert por chave natural com dedup intra-fonte: chaves repetidas na própria
    // carga não disparam unique violation — a 2ª ocorrência atualiza a entidade já
    // vista (last-wins) e conta como duplicada. Retorna a entidade (para resolver FK)
    // ou null em falha de validação do domínio.
    private static T? Upsert<T>(
        string chave,
        Dictionary<string, T> jaVistas,
        Dictionary<string, T> existentes,
        Func<Result<T>> criar,
        Func<T, Result> atualizar,
        DbSet<T> dbSet,
        ContadorTabela contador)
        where T : class
    {
        if (jaVistas.TryGetValue(chave, out T? jaVista))
        {
            contador.ContarDuplicado(chave);
            Result reatualizacao = atualizar(jaVista);
            return reatualizacao.IsSuccess ? jaVista : null;
        }

        if (existentes.TryGetValue(chave, out T? existente))
        {
            Result atualizacao = atualizar(existente);
            if (atualizacao.IsFailure)
            {
                contador.ContarIgnoradoSemChave();
                return null;
            }

            jaVistas[chave] = existente;
            contador.ContarAtualizado();
            return existente;
        }

        Result<T> criado = criar();
        if (criado.IsFailure)
        {
            contador.ContarIgnoradoSemChave();
            return null;
        }

        dbSet.Add(criado.Value!);
        jaVistas[chave] = criado.Value!;
        contador.ContarInserido();
        return criado.Value;
    }

    private static async Task<Dictionary<string, T>> AgregarPorChaveAsync<T>(
        IAsyncEnumerable<T> fonte,
        Func<T, string?> chave)
    {
        Dictionary<string, T> mapa = new(StringComparer.Ordinal);
        await foreach (T item in fonte.ConfigureAwait(false))
        {
            string? k = chave(item);
            if (k is not null)
            {
                mapa[k] = item; // last-wins
            }
        }

        return mapa;
    }

    // Como AgregarPorChaveAsync, mas registra no contador da tabela tudo o que a fonte
    // trouxe (lidos), o que foi descartado por chave ausente e as chaves repetidas
    // (last-wins) — para o relatório não subestimar os "lidos" dos níveis agregados.
    private static async Task<Dictionary<string, T>> AgregarContandoAsync<T>(
        IAsyncEnumerable<T> fonte,
        Func<T, string?> chave,
        ContadorTabela contador)
    {
        Dictionary<string, T> mapa = new(StringComparer.Ordinal);
        await foreach (T item in fonte.ConfigureAwait(false))
        {
            contador.ContarLido();
            string? k = chave(item);
            if (k is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            if (!mapa.TryAdd(k, item))
            {
                contador.ContarDuplicado(k); // last-wins
                mapa[k] = item;
            }
        }

        return mapa;
    }

    // Parse tolerante de métrica IBGE que registra a degradação ('-'/vazio/inválido →
    // null) no relatório para auditoria. Sem PII — métricas socioeconômicas públicas.
    private static decimal? MetricaDecimal(string? cru, string coluna, ContadorTabela contador)
    {
        decimal? valor = ParseTolerante.ParaDecimal(cru);
        if (valor is null && !string.IsNullOrWhiteSpace(cru))
        {
            contador.ContarParseDegradado($"{coluna}={cru.Trim()}");
        }

        return valor;
    }

    private static int? MetricaInteira(string? cru, string coluna, ContadorTabela contador)
    {
        int? valor = ParseTolerante.ParaInteiro(cru);
        if (valor is null && !string.IsNullOrWhiteSpace(cru))
        {
            contador.ContarParseDegradado($"{coluna}={cru.Trim()}");
        }

        return valor;
    }

    private static string? ChaveSigla(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    private static string? ChaveCodigo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // Chave natural composta de uma faixa de CEP: pai (Guid) + limites. Casa com a
    // UNIQUE (estado_id|cidade_id, cep_inicial, cep_final) para o upsert idempotente.
    private static string ChaveFaixa(Guid pai, string cepInicial, string cepFinal) =>
        $"{pai:N}|{cepInicial}|{cepFinal}";

    private static int Total(RelatorioImportacao relatorio, Func<ContadorTabela, int> seletor) =>
        relatorio.Tabelas.Values.Sum(seletor);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga iniciada (versão {Versao}).")]
    private static partial void LogCargaIniciada(ILogger logger, string versao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: país-âncora 'BRA' ausente no dataset da versão {Versao} — nenhum estado/cidade será carregado.")]
    private static partial void LogBrasilAusente(ILogger logger, string versao);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: tabela {Tabela} concluída (lidos={Lidos}, inseridos={Inseridos}, atualizados={Atualizados}).")]
    private static partial void LogTabelaConcluida(ILogger logger, string tabela, int lidos, int inseridos, int atualizados);

    [LoggerMessage(Level = LogLevel.Information, Message = "ETL Geo: carga concluída (versão {Versao}, inseridos={Inseridos}, atualizados={Atualizados}, órfãos={Orfaos}, degradados={Degradados}).")]
    private static partial void LogCargaConcluida(ILogger logger, string versao, int inseridos, int atualizados, int orfaos, int degradados);
}
