namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Protege o CPF e os demais números de documento na fronteira do repositório (ADR-0121, Opção
/// C): o EF Core só materializa records; a tradução para/de <see cref="Inscricao"/> — incluindo
/// cifrar/decifrar via <see cref="IUniPlusEncryptionService"/> e computar o índice cego via
/// <see cref="IUniPlusBlindIndexService"/>, ambos assíncronos — acontece inteiramente aqui, nunca
/// num <c>ValueConverter</c> síncrono.
/// </summary>
/// <remarks>
/// As respostas ficam uma linha por valor (ADR-0141), guiadas pela definição de cada fato. A gravação
/// sincroniza as linhas pela chave natural — ocorrência, fato e posição —, então uma linha que não
/// mudou continua a mesma e nenhuma colide com o índice único dentro da mesma gravação.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em SelecaoInfrastructureRegistration.")]
public sealed class InscricaoRepository : IInscricaoRepository
{
    private readonly SelecaoDbContext _dbContext;
    private readonly IUniPlusEncryptionService _encryption;
    private readonly IUniPlusBlindIndexService _blindIndex;
    private readonly TimeProvider _timeProvider;

    public InscricaoRepository(
        SelecaoDbContext dbContext,
        IUniPlusEncryptionService encryption,
        IUniPlusBlindIndexService blindIndex,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(blindIndex);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _encryption = encryption;
        _blindIndex = blindIndex;
        _timeProvider = timeProvider;
    }

    public async Task<Inscricao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        InscricaoRecord? record = await _dbContext.Inscricoes
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : (await ParaDominioAsync([record], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<Inscricao?> ObterAtivaPorProcessoECpfAsync(
        Guid processoSeletivoId, Cpf cpf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        byte[] indice = await ComputarIndiceAsync(cpf, cancellationToken).ConfigureAwait(false);

        InscricaoRecord? record = await _dbContext.Inscricoes
            .FirstOrDefaultAsync(
                r => r.ProcessoSeletivoId == processoSeletivoId && r.Situacao != SituacaoInscricao.Cancelada && r.CpfIndice.SequenceEqual(indice),
                cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : (await ParaDominioAsync([record], cancellationToken).ConfigureAwait(false))[0];
    }

    public async Task<IReadOnlyList<Inscricao>> ObterTodasPorCpfAsync(Cpf cpf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        byte[] indice = await ComputarIndiceAsync(cpf, cancellationToken).ConfigureAwait(false);

        List<InscricaoRecord> records = await _dbContext.Inscricoes
            .Where(r => r.CpfIndice.SequenceEqual(indice))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await ParaDominioAsync(records, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = new() { Id = entity.Id, CreatedAt = _timeProvider.GetUtcNow() };
        LinhasDaInscricao desejadas = await LinhasDeAsync(entity, cancellationToken).ConfigureAwait(false);
        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);

        _dbContext.Inscricoes.Add(record);
        Sincronizar(LinhasDaInscricao.Vazias, desejadas);
    }

    public async Task AtualizarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = await _dbContext.Inscricoes
            .FirstAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        LinhasDaInscricao desejadas = await LinhasDeAsync(entity, cancellationToken).ConfigureAwait(false);
        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        record.UpdatedAt = _timeProvider.GetUtcNow();

        LinhasDaInscricao atuais = await CarregarLinhasAsync([entity.Id], cancellationToken).ConfigureAwait(false);
        Sincronizar(atuais, desejadas);
    }

    public async Task RemoverAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Duas remoções do mesmo rascunho (duplo clique): a segunda não acha a linha, e isso é conflito de
        // concorrência — o middleware o responde como 409 —, não falha do servidor.
        InscricaoRecord record = await _dbContext.Inscricoes
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DbUpdateConcurrencyException($"A inscrição {entity.Id} já foi removida por outra requisição.");

        // As linhas filhas caem na própria remoção: a chave estrangeira de cada uma é em cascata.
        _dbContext.Inscricoes.Remove(record);
    }

    public async Task<IReadOnlyList<ComprovanteInscricao>> ObterComprovantesAsync(
        IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inscricoes);

        List<ComprovanteInscricao> comprovantes = await _dbContext.ComprovantesInscricao
            .AsNoTracking()
            .Where(c => inscricoes.Contains(c.InscricaoId))
            // O número público do comprovante é a posição nesta lista: o Id (UUIDv7, gerado na
            // emissão) desempata dois comprovantes do mesmo instante, e a ordem fica estável.
            .OrderBy(static c => c.Instante)
            .ThenBy(static c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (ComprovanteInscricao comprovante in comprovantes.Where(static c => c.SensiveisCifrados is not null || c.SensiveisHmac is not null))
        {
            if (comprovante.SensiveisCifrados is null || comprovante.SensiveisHmac is null)
            {
                throw new InvalidOperationException($"O comprovante {comprovante.Id} tem só uma das colunas dos dados sensíveis: o envelope cifrado e o HMAC andam juntos.");
            }

            byte[] plano = await _encryption
                .DecryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, comprovante.SensiveisCifrados!, cancellationToken)
                .ConfigureAwait(false);

            // O envelope cifrado é autêntico, mas nada o prende a este comprovante: o HMAC de id e valores,
            // gravado ao lado, acusa o envelope copiado de outro comprovante (falha alto, como o CPF corrompido).
            byte[] esperado = await HmacDosSensiveisAsync(comprovante.Id, plano, cancellationToken).ConfigureAwait(false);
            if (comprovante.SensiveisHmac is not { } gravado || !CryptographicOperations.FixedTimeEquals(gravado, esperado))
            {
                throw new InvalidOperationException($"As respostas sensíveis do comprovante {comprovante.Id} não pertencem a ele: o vínculo não confere.");
            }

            comprovante.RestaurarSensiveis(
                JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(plano)
                ?? throw new InvalidOperationException($"As respostas sensíveis do comprovante {comprovante.Id} decifradas não são um objeto JSON."));

            // Os fatos restaurados têm de ser os que o hash do comprovante cobre.
            if (!comprovante.IntegridadeConfere())
            {
                throw new InvalidOperationException($"Os fatos sensíveis restaurados do comprovante {comprovante.Id} não são os que o hash dele cobre.");
            }
        }

        return comprovantes;
    }

    public async Task<bool> TentarAvancarRevisaoAsync(
        Guid id, int revisaoLida, bool apenasRascunho, CancellationToken cancellationToken = default)
    {
        DateTimeOffset agora = _timeProvider.GetUtcNow();
        int linhasAfetadas = await _dbContext.Inscricoes
            .Where(r => r.Id == id && r.Revisao == revisaoLida)
            .Where(r => apenasRascunho ? r.Situacao == SituacaoInscricao.Rascunho : r.Situacao != SituacaoInscricao.Cancelada)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.Revisao, revisaoLida + 1).SetProperty(r => r.UpdatedAt, agora),
                cancellationToken)
            .ConfigureAwait(false);

        return linhasAfetadas == 1;
    }

    public async Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comprovante);

        if (comprovante.RespostasSensiveis.Count > 0)
        {
            byte[] plano = JsonSerializer.SerializeToUtf8Bytes(comprovante.RespostasSensiveis);
            byte[] cifrados = await _encryption
                .EncryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, plano, cancellationToken)
                .ConfigureAwait(false);
            comprovante.GuardarSensiveisCifrados(
                cifrados, await HmacDosSensiveisAsync(comprovante.Id, plano, cancellationToken).ConfigureAwait(false));
        }

        _dbContext.ComprovantesInscricao.Add(comprovante);
    }

    /// <summary>O HMAC do id do comprovante e dos valores sensíveis, exatamente como foram cifrados.</summary>
    private Task<byte[]> HmacDosSensiveisAsync(Guid comprovanteId, byte[] valores, CancellationToken cancellationToken) =>
        _blindIndex.ComputarAsync(
            SelecaoEncryptionKeys.SensiveisHmac,
            (byte[])[.. Encoding.UTF8.GetBytes($"comprovante:{comprovanteId:D}:"), .. valores],
            cancellationToken);

    /// <summary>A chave da categoria da resposta cifrada: documento ou dado sensível de cifra individual.</summary>
    private static string ChaveDaResposta(string fatoCodigo) =>
        DefinicaoDaResposta.EhFatoComCifraIndividual(fatoCodigo) ? SelecaoEncryptionKeys.SensiveisAesGcm : SelecaoEncryptionKeys.IdentificadoresAesGcm;

    private async Task<List<Inscricao>> ParaDominioAsync(List<InscricaoRecord> records, CancellationToken cancellationToken)
    {
        LinhasDaInscricao linhas = await CarregarLinhasAsync([.. records.Select(static r => r.Id)], cancellationToken).ConfigureAwait(false);

        List<Inscricao> inscricoes = new(records.Count);
        foreach (InscricaoRecord record in records)
        {
            inscricoes.Add(await ParaDominioAsync(record, linhas.Da(record.Id), cancellationToken).ConfigureAwait(false));
        }

        return inscricoes;
    }

    private async Task<Inscricao> ParaDominioAsync(InscricaoRecord record, LinhasDaInscricao linhas, CancellationToken cancellationToken)
    {
        byte[] cpfPlano = await _encryption
            .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, record.CpfCiphertext, cancellationToken)
            .ConfigureAwait(false);
        Cpf cpf = ReidratarCpf(cpfPlano);

        Dictionary<Guid, OcorrenciaDeGrupoDaInscricaoRecord> ocorrencias = linhas.Ocorrencias.ToDictionary(static o => o.Id);
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        Dictionary<string, JsonElement> derivados = new(StringComparer.Ordinal);
        Dictionary<Guid, Dictionary<string, JsonElement>> respostasPorOcorrencia = ocorrencias.Keys
            .ToDictionary(static id => id, static _ => new Dictionary<string, JsonElement>(StringComparer.Ordinal));
        Dictionary<string, DefinicaoDaResposta> definicoes = new(StringComparer.Ordinal);

        Dictionary<string, JsonElement> Destino(Guid? ocorrenciaId, OrigemDaResposta origem) =>
            ocorrenciaId is { } id ? respostasPorOcorrencia[id] : origem == OrigemDaResposta.Derivada ? derivados : respostas;

        foreach (IGrouping<(Guid? OcorrenciaId, string FatoCodigo), RespostaDaInscricaoRecord> fato in linhas.Respostas
            .GroupBy(static r => (r.OcorrenciaId, r.FatoCodigo)))
        {
            RespostaDaInscricaoRecord primeira = fato.First();
            bool multivalorado = primeira.Posicao is not null;
            List<JsonElement> valores = [];
            foreach (RespostaDaInscricaoRecord linha in fato.OrderBy(static r => r.Posicao))
            {
                valores.Add(await ValorDaLinhaAsync(linha, cancellationToken).ConfigureAwait(false));
            }

            Destino(fato.Key.OcorrenciaId, primeira.Origem)[fato.Key.FatoCodigo] =
                multivalorado ? JsonSerializer.SerializeToElement(valores) : valores[0];
            bool cifrada = primeira.ValorCifrado is not null;
            bool cifraIndividual = cifrada && DefinicaoDaResposta.EhFatoComCifraIndividual(fato.Key.FatoCodigo);
            definicoes.TryAdd(fato.Key.FatoCodigo, new DefinicaoDaResposta(primeira.Tipo, multivalorado, cifrada && !cifraIndividual, null, cifraIndividual));
        }

        foreach (EnderecoDaInscricaoRecord endereco in linhas.Enderecos)
        {
            Destino(endereco.OcorrenciaId, endereco.Origem)[endereco.FatoCodigo] = ParaEndereco(endereco).ParaJson();
            definicoes.TryAdd(endereco.FatoCodigo, new DefinicaoDaResposta(TipoDoValorDeResposta.Endereco, Multivalorado: false, Identificador: false));
        }

        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = linhas.Ocorrencias
            .OrderBy(static o => o.Ordem)
            .GroupBy(static o => o.GrupoCodigo, StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                    [.. g.Select(o => new OcorrenciaDeGrupoInscricao(o.MembroId, respostasPorOcorrencia[o.Id]))],
                StringComparer.Ordinal);

        List<AceiteDeTermo> aceites =
        [
            .. linhas.Aceites.OrderBy(static a => a.Ordem).Select(static a => new AceiteDeTermo(a.TermoCodigo, a.VersaoId, a.HashVersao)),
        ];

        return Inscricao.Reidratar(
            record.Id, record.ProcessoSeletivoId, cpf, record.Sub, record.EmissorToken, record.Situacao,
            record.VersaoFormularioId, record.OfertaCursoId, record.OfertaCursoSegundaOpcaoId, record.NumeroInscricao,
            record.CanceladaEm, record.CanceladaPor, record.MotivoDoCancelamento,
            respostas, derivados, grupos, aceites, definicoes,
            record.Revisao, record.CreatedAt, record.UpdatedAt);
    }

    private async Task<JsonElement> ValorDaLinhaAsync(RespostaDaInscricaoRecord linha, CancellationToken cancellationToken)
    {
        if (linha.ValorCifrado is { } cifrado)
        {
            byte[] plano = await _encryption
                .DecryptAsync(ChaveDaResposta(linha.FatoCodigo), cifrado, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<JsonElement>(plano);
        }

        return linha.Tipo switch
        {
            TipoDoValorDeResposta.Booleano => JsonSerializer.SerializeToElement(linha.ValorBooleano!.Value),
            TipoDoValorDeResposta.Numero => JsonSerializer.SerializeToElement(linha.ValorNumero!.Value),
            TipoDoValorDeResposta.Codigo => JsonSerializer.SerializeToElement(linha.ValorCodigo!),
            TipoDoValorDeResposta.Texto => JsonSerializer.SerializeToElement(linha.ValorTexto!),
            TipoDoValorDeResposta.Data => JsonSerializer.SerializeToElement(linha.ValorData!.Value),
            _ => throw new InvalidOperationException($"A resposta {linha.Id} tem um tipo sem coluna de valor ({linha.Tipo})."),
        };
    }

    private static EnderecoDeclarado ParaEndereco(EnderecoDaInscricaoRecord e) => new(
        e.Cep, e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Distrito,
        e.MunicipioCodigoIbge, e.MunicipioNome, e.Uf, e.Latitude, e.Longitude, e.NivelResolucao, e.Procedencia, e.CepConfirmado);

    /// <summary>
    /// As linhas que a inscrição deve ter. Cifra antes de qualquer mutação do que o ChangeTracker
    /// rastreia: se a cifragem falhar, nada fica pela metade para um <c>SaveChangesAsync</c> gravar.
    /// </summary>
    private async Task<LinhasDaInscricao> LinhasDeAsync(Inscricao entity, CancellationToken cancellationToken)
    {
        LinhasDaInscricao linhas = new([], [], [], []);

        await AcrescentarAsync(linhas, entity, entity.Respostas, null, OrigemDaResposta.Informada, cancellationToken).ConfigureAwait(false);
        await AcrescentarAsync(linhas, entity, entity.Derivados, null, OrigemDaResposta.Derivada, cancellationToken).ConfigureAwait(false);

        int ordem = 0;
        foreach ((string grupoCodigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in entity.Grupos)
        {
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                OcorrenciaDeGrupoDaInscricaoRecord registro = new()
                {
                    Id = Guid.CreateVersion7(),
                    InscricaoId = entity.Id,
                    GrupoCodigo = grupoCodigo,
                    MembroId = ocorrencia.Id,
                    Ordem = ordem++,
                };
                linhas.Ocorrencias.Add(registro);
                await AcrescentarAsync(linhas, entity, ocorrencia.Respostas, registro.Id, OrigemDaResposta.Informada, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        linhas.Aceites.AddRange(entity.Aceites.Select((a, i) => new AceiteDaInscricaoRecord
        {
            Id = Guid.CreateVersion7(),
            InscricaoId = entity.Id,
            Ordem = i,
            TermoCodigo = a.Codigo,
            VersaoId = a.VersaoId,
            HashVersao = a.HashVersao,
        }));

        return linhas;
    }

    private async Task AcrescentarAsync(
        LinhasDaInscricao linhas,
        Inscricao entity,
        IReadOnlyDictionary<string, JsonElement> respostas,
        Guid? ocorrenciaId,
        OrigemDaResposta origem,
        CancellationToken cancellationToken)
    {
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            if (!entity.DefinicoesDasRespostas.TryGetValue(codigo, out DefinicaoDaResposta? definicao))
            {
                throw new InvalidOperationException($"A inscrição {entity.Id} tem resposta de '{codigo}' sem definição da resposta.");
            }

            if (definicao.Tipo == TipoDoValorDeResposta.Endereco)
            {
                if (definicao.Identificador || !EnderecoDeclarado.TentarLer(valor, out EnderecoDeclarado endereco))
                {
                    throw new InvalidOperationException($"A resposta de '{codigo}' da inscrição {entity.Id} não é um endereço gravável.");
                }

                linhas.Enderecos.Add(new EnderecoDaInscricaoRecord
                {
                    Id = Guid.CreateVersion7(),
                    InscricaoId = entity.Id,
                    OcorrenciaId = ocorrenciaId,
                    FatoCodigo = codigo,
                    Origem = origem,
                    Cep = endereco.Cep,
                    Logradouro = endereco.Logradouro,
                    Numero = endereco.Numero,
                    Complemento = endereco.Complemento,
                    Bairro = endereco.Bairro,
                    Distrito = endereco.Distrito,
                    MunicipioCodigoIbge = endereco.CidadeCodigoIbge,
                    MunicipioNome = endereco.CidadeNome,
                    Uf = endereco.CidadeUf,
                    Latitude = endereco.Latitude,
                    Longitude = endereco.Longitude,
                    NivelResolucao = endereco.NivelResolucao,
                    Procedencia = endereco.Procedencia,
                    CepConfirmado = endereco.CepConfirmado,
                });
                continue;
            }

            JsonElement[] valores = definicao.Multivalorado ? [.. valor.EnumerateArray()] : [valor];
            for (int posicao = 0; posicao < valores.Length; posicao++)
            {
                RespostaDaInscricaoRecord linha = new()
                {
                    Id = Guid.CreateVersion7(),
                    InscricaoId = entity.Id,
                    OcorrenciaId = ocorrenciaId,
                    FatoCodigo = codigo,
                    Posicao = definicao.Multivalorado ? posicao : null,
                    Origem = origem,
                    Tipo = definicao.Tipo,
                };
                await PreencherValorAsync(linha, valores[posicao], definicao, cancellationToken).ConfigureAwait(false);
                linhas.Respostas.Add(linha);
            }
        }
    }

    private async Task PreencherValorAsync(RespostaDaInscricaoRecord linha, JsonElement valor, DefinicaoDaResposta definicao, CancellationToken cancellationToken)
    {
        if (definicao.Cifrada)
        {
            linha.ValorCifrado = await _encryption
                .EncryptAsync(ChaveDaResposta(linha.FatoCodigo), Encoding.UTF8.GetBytes(valor.GetRawText()), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        switch (definicao.Tipo)
        {
            case TipoDoValorDeResposta.Booleano:
                linha.ValorBooleano = valor.GetBoolean();
                break;
            case TipoDoValorDeResposta.Numero:
                linha.ValorNumero = valor.GetDecimal();
                break;
            case TipoDoValorDeResposta.Codigo:
                linha.ValorCodigo = valor.GetString();
                break;
            case TipoDoValorDeResposta.Texto:
                linha.ValorTexto = valor.GetString();
                break;
            case TipoDoValorDeResposta.Data:
                linha.ValorData = DateOnly.ParseExact(valor.GetString()!, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                break;
            default:
                throw new InvalidOperationException($"O tipo {definicao.Tipo} não tem coluna de valor.");
        }
    }

    private async Task<LinhasDaInscricao> CarregarLinhasAsync(IReadOnlyCollection<Guid> inscricoes, CancellationToken cancellationToken)
    {
        List<RespostaDaInscricaoRecord> respostas = await _dbContext.RespostasDaInscricao
            .Where(r => inscricoes.Contains(r.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<EnderecoDaInscricaoRecord> enderecos = await _dbContext.EnderecosDaInscricao
            .Where(e => inscricoes.Contains(e.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<OcorrenciaDeGrupoDaInscricaoRecord> ocorrencias = await _dbContext.OcorrenciasDeGrupoDaInscricao
            .Where(o => inscricoes.Contains(o.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<AceiteDaInscricaoRecord> aceites = await _dbContext.AceitesDaInscricao
            .Where(a => inscricoes.Contains(a.InscricaoId)).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new LinhasDaInscricao(respostas, enderecos, ocorrencias, aceites);
    }

    /// <summary>
    /// Leva as linhas atuais às desejadas: a ocorrência casa pelo grupo e pelo membro, a resposta
    /// pela ocorrência, pelo fato e pela posição, o aceite pela ordem.
    /// </summary>
    private void Sincronizar(LinhasDaInscricao atuais, LinhasDaInscricao desejadas)
    {
        Dictionary<Guid, Guid> idDaOcorrencia = [];
        Dictionary<(string, string), OcorrenciaDeGrupoDaInscricaoRecord> ocorrenciasAtuais =
            atuais.Ocorrencias.ToDictionary(static o => (o.GrupoCodigo, o.MembroId));
        foreach (OcorrenciaDeGrupoDaInscricaoRecord desejada in desejadas.Ocorrencias)
        {
            if (ocorrenciasAtuais.Remove((desejada.GrupoCodigo, desejada.MembroId), out OcorrenciaDeGrupoDaInscricaoRecord? atual))
            {
                atual.Ordem = desejada.Ordem;
                idDaOcorrencia[desejada.Id] = atual.Id;
            }
            else
            {
                _dbContext.OcorrenciasDeGrupoDaInscricao.Add(desejada);
                idDaOcorrencia[desejada.Id] = desejada.Id;
            }
        }

        foreach (RespostaDaInscricaoRecord resposta in desejadas.Respostas.Where(static r => r.OcorrenciaId is not null))
        {
            resposta.OcorrenciaId = idDaOcorrencia[resposta.OcorrenciaId!.Value];
        }

        foreach (EnderecoDaInscricaoRecord endereco in desejadas.Enderecos.Where(static e => e.OcorrenciaId is not null))
        {
            endereco.OcorrenciaId = idDaOcorrencia[endereco.OcorrenciaId!.Value];
        }

        SincronizarPorChave(
            _dbContext.RespostasDaInscricao, atuais.Respostas, desejadas.Respostas,
            static r => (r.OcorrenciaId, r.FatoCodigo, r.Posicao),
            static (de, para) =>
            {
                para.Origem = de.Origem;
                para.Tipo = de.Tipo;
                para.ValorBooleano = de.ValorBooleano;
                para.ValorNumero = de.ValorNumero;
                para.ValorCodigo = de.ValorCodigo;
                para.ValorTexto = de.ValorTexto;
                para.ValorData = de.ValorData;
                para.ValorCifrado = de.ValorCifrado;
            });

        SincronizarPorChave(
            _dbContext.EnderecosDaInscricao, atuais.Enderecos, desejadas.Enderecos,
            static e => (e.OcorrenciaId, e.FatoCodigo),
            static (de, para) =>
            {
                para.Origem = de.Origem;
                para.Cep = de.Cep;
                para.Logradouro = de.Logradouro;
                para.Numero = de.Numero;
                para.Complemento = de.Complemento;
                para.Bairro = de.Bairro;
                para.Distrito = de.Distrito;
                para.MunicipioCodigoIbge = de.MunicipioCodigoIbge;
                para.MunicipioNome = de.MunicipioNome;
                para.Uf = de.Uf;
                para.Latitude = de.Latitude;
                para.Longitude = de.Longitude;
                para.NivelResolucao = de.NivelResolucao;
                para.Procedencia = de.Procedencia;
                para.CepConfirmado = de.CepConfirmado;
            });

        SincronizarPorChave(
            _dbContext.AceitesDaInscricao, atuais.Aceites, desejadas.Aceites,
            static a => a.Ordem,
            static (de, para) =>
            {
                para.TermoCodigo = de.TermoCodigo;
                para.VersaoId = de.VersaoId;
                para.HashVersao = de.HashVersao;
            });

        _dbContext.OcorrenciasDeGrupoDaInscricao.RemoveRange(ocorrenciasAtuais.Values);
    }

    private static void SincronizarPorChave<TRecord, TChave>(
        DbSet<TRecord> conjunto,
        IReadOnlyList<TRecord> atuais,
        IReadOnlyList<TRecord> desejadas,
        Func<TRecord, TChave> chave,
        Action<TRecord, TRecord> copiar)
        where TRecord : class
        where TChave : notnull
    {
        Dictionary<TChave, TRecord> porChave = atuais.ToDictionary(chave);
        foreach (TRecord desejada in desejadas)
        {
            if (porChave.Remove(chave(desejada), out TRecord? atual))
            {
                copiar(desejada, atual);
            }
            else
            {
                conjunto.Add(desejada);
            }
        }

        conjunto.RemoveRange(porChave.Values);
    }

    /// <summary>
    /// Não toca <c>CreatedAt</c>/<c>UpdatedAt</c>: quem o EF rastreia é <see cref="InscricaoRecord"/>,
    /// que não herda <c>EntityBase</c> — o carimbo é de quem chama.
    /// </summary>
    private async Task AtualizarCamposAsync(InscricaoRecord record, Inscricao entity, CancellationToken cancellationToken)
    {
        byte[] cpfCiphertext = await _encryption
            .EncryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, Encoding.UTF8.GetBytes(entity.Cpf.Valor), cancellationToken)
            .ConfigureAwait(false);
        byte[] cpfIndice = await ComputarIndiceAsync(entity.Cpf, cancellationToken).ConfigureAwait(false);

        record.ProcessoSeletivoId = entity.ProcessoSeletivoId;
        record.CpfCiphertext = cpfCiphertext;
        record.CpfIndice = cpfIndice;
        record.Sub = entity.Sub;
        record.EmissorToken = entity.EmissorToken;
        record.Situacao = entity.Situacao;
        record.VersaoFormularioId = entity.VersaoFormularioId;
        record.OfertaCursoId = entity.OfertaCursoId;
        record.OfertaCursoSegundaOpcaoId = entity.OfertaCursoSegundaOpcaoId;
        record.NumeroInscricao = entity.NumeroInscricao;
        record.CanceladaEm = entity.CanceladaEm;
        record.CanceladaPor = entity.CanceladaPor;
        record.MotivoDoCancelamento = entity.MotivoDoCancelamento;
        record.Revisao = entity.Revisao;
    }

    private Task<byte[]> ComputarIndiceAsync(Cpf cpf, CancellationToken cancellationToken) =>
        _blindIndex.ComputarAsync(
            SelecaoEncryptionKeys.IdentificadoresHmac, Encoding.UTF8.GetBytes($"cpf:{cpf.Valor}"), cancellationToken);

    /// <summary>
    /// Revalida o CPF decifrado contra a regra de domínio — dado corrompido no banco ou chave de
    /// cifragem incorreta falha alto, em vez de produzir um <see cref="Cpf"/> inválido em silêncio
    /// (mesmo padrão de <c>VinculoDiscenteRepository</c>).
    /// </summary>
    private static Cpf ReidratarCpf(byte[] cpfPlano)
    {
        string valor = Encoding.UTF8.GetString(cpfPlano);
        return Cpf.Criar(valor).Match(
            cpf => cpf,
            erro => throw new InvalidOperationException($"CPF decifrado da tabela inscricoes falhou a revalidação de domínio ({erro.Code})."));
    }

    private sealed record LinhasDaInscricao(
        List<RespostaDaInscricaoRecord> Respostas,
        List<EnderecoDaInscricaoRecord> Enderecos,
        List<OcorrenciaDeGrupoDaInscricaoRecord> Ocorrencias,
        List<AceiteDaInscricaoRecord> Aceites)
    {
        public static LinhasDaInscricao Vazias => new([], [], [], []);

        public LinhasDaInscricao Da(Guid inscricaoId) => new(
            [.. Respostas.Where(r => r.InscricaoId == inscricaoId)],
            [.. Enderecos.Where(e => e.InscricaoId == inscricaoId)],
            [.. Ocorrencias.Where(o => o.InscricaoId == inscricaoId)],
            [.. Aceites.Where(a => a.InscricaoId == inscricaoId)]);
    }
}
