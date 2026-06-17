namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;

// Registros "crus" (varchar da fonte DNE) lidos pela IGeoFonteDados antes do parse
// tolerante. Espelham apenas as colunas que o importador de País/Estado/Cidade usa
// (Epic §9). Identificadores ficam string crua; métricas são parseadas no importador.
// Nomes são semânticos do domínio Uni+ (a fonte usa rótulos como "estado" para a UF).

/// <summary>País (fonte <c>paises_ibge</c>) — só o registro <c>BRA</c> é carregado.</summary>
internal sealed record PaisCru(
    string? SiglaIso,
    string? Sigla,
    string? Nome,
    string? CodigoBcb,
    string? CodigoRfb,
    string? CodigoSped,
    string? CodigoSiscomex);

/// <summary>Estado/UF (fonte <c>estado</c>): chave natural <c>sigla</c>; faixa geral em <c>faixa_ini/fim</c>.</summary>
internal sealed record EstadoCru(
    string? Uf,
    string? Nome,
    string? NomeSemAcento,
    string? Regiao,
    string? Capital,
    string? FaixaIni,
    string? FaixaFim,
    string? Latitude,
    string? Longitude);

/// <summary>Indicadores socioeconômicos do Estado (fonte <c>estado_ibge</c>, 1:1 por <c>uf</c>).</summary>
internal sealed record EstadoIndicadorCru(
    string? Uf,
    string? CodigoIbge,
    string? Gentilico,
    string? Governador,
    string? AreaTerritorialKm2,
    string? PopulacaoResidente2022,
    string? DensidadeDemografica,
    string? MatriculasEnsinoFundamental2023,
    string? Idh,
    string? ReceitasBrutas,
    string? DespesasBrutas,
    string? RendimentoMensalPerCapita,
    string? TotalVeiculos2023);

/// <summary>Faixa de CEP do Estado (fonte <c>estado_faixa</c>): <c>descricao</c> = <c>regiao</c>.</summary>
internal sealed record EstadoFaixaCru(
    string? Uf,
    string? Descricao,
    string? FaixaIni,
    string? FaixaFim);

/// <summary>Município (fonte <c>cidade</c>): chave natural <c>cidade_ibge</c>; vínculo por <c>estado</c> (UF).</summary>
internal sealed record CidadeCru(
    string? CodigoIbge,
    string? Nome,
    string? NomeSemAcento,
    string? Uf,
    string? Ddd,
    string? Latitude,
    string? Longitude);

/// <summary>Recorte territorial IBGE do município (fonte <c>cidade_ibge_territorio</c>, casa por <c>cidade_ibge</c>).</summary>
internal sealed record CidadeTerritorioCru(
    string? CodigoIbge,
    string? MesorregiaoCodigo,
    string? MesorregiaoNome,
    string? MicrorregiaoCodigo,
    string? MicrorregiaoNome,
    string? RegiaoIntermediariaCodigo,
    string? RegiaoIntermediariaNome,
    string? RegiaoImediataCodigo,
    string? RegiaoImediataNome);

/// <summary>Indicadores socioeconômicos do município (fonte <c>cidade_ibge</c>, casa por <c>cidade_ibge</c>).</summary>
internal sealed record CidadeIndicadorCru(
    string? CodigoIbge,
    string? Gentilico,
    string? Prefeito,
    string? AreaTerritorialKm2,
    string? PopulacaoResidente,
    string? DensidadeDemografica,
    string? Escolarizacao6a14,
    string? Idh,
    string? MortalidadeInfantil,
    string? Receitas,
    string? Despesas,
    string? PibPerCapita,
    string? Aniversario);

/// <summary>Faixa de CEP do município (fonte <c>cidade_faixa</c>, casa por <c>cidade_ibge</c>).</summary>
internal sealed record CidadeFaixaCru(
    string? CodigoIbge,
    string? FaixaIni,
    string? FaixaFim);

// ----------------------------------------------------------------------------
// Folhas da hierarquia (Story #673): Distrito, Bairro, Logradouro e satélites.
// Os ids da fonte são int4 (não varchar) — instáveis entre releases, usados só
// para resolver as FKs intra-release para Guid (ADR-0054); o domínio nunca os
// adota como identidade.
// ----------------------------------------------------------------------------

/// <summary>Mapa <c>id_cidade</c> (int4, PK da fonte) → <c>cidade_ibge</c> — resolve a FK <c>cidade_id</c> para Guid.</summary>
internal sealed record CidadeIdCru(
    int? IdCidade,
    string? CodigoIbge);

/// <summary>Distrito (fonte <c>distrito</c>): PK <c>id_distrito</c>; FK <c>cidade_id</c> (int4); chave natural de upsert <c>(cidade, nome_sem_acento)</c>.</summary>
internal sealed record DistritoCru(
    int? IdDistrito,
    string? Nome,
    string? NomeSemAcento,
    int? CidadeIdDne,
    string? Uf,
    string? Latitude,
    string? Longitude);

/// <summary>Bairro (fonte <c>bairro</c>): PK <c>id_bairro</c>; FK <c>cidade_id</c> (int4); chave natural de upsert <c>(cidade, nome_sem_acento)</c>.</summary>
internal sealed record BairroCru(
    int? IdBairro,
    string? Nome,
    string? NomeSemAcento,
    int? CidadeIdDne,
    string? Uf,
    string? Latitude,
    string? Longitude);

/// <summary>Faixa de CEP de Distrito/Bairro (fontes <c>distrito_faixa</c>/<c>bairro_faixa</c>): vínculo pelo id int4 do pai.</summary>
internal sealed record FaixaLocalidadeCru(
    int? IdPaiDne,
    string? FaixaIni,
    string? FaixaFim);

/// <summary>CEP exclusivo de grande usuário (fonte <c>log_grande_usuario</c>): sem cidade/UF.</summary>
internal sealed record CepGrandeUsuarioCru(
    string? Cep,
    string? Nome,
    string? NomeSemAcento);

/// <summary>Complemento por CEP (fonte <c>log_complemento</c>): sem FK a logradouro.</summary>
internal sealed record LogradouroComplementoCru(
    string? Cep,
    string? Complemento,
    string? ComplementoSemAcento);

/// <summary>Logradouro (fonte <c>logradouro</c>, ~1,4M linhas): FK <c>cidade_id</c> (obrigatória) e <c>distrito_id</c>/<c>bairro_id</c> (int4 opcionais, NULL frequente); <c>cep_ativo</c> 'S'/'N'.</summary>
internal sealed record LogradouroCru(
    string? Cep,
    string? Tipo,
    string? Nome,
    string? NomeCompleto,
    string? NomeSemAcento,
    int? BairroIdDne,
    int? DistritoIdDne,
    int? CidadeIdDne,
    string? Uf,
    string? Latitude,
    string? Longitude,
    string? CepAtivo);
