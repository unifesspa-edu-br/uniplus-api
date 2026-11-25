namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
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
public sealed class InscricaoRepository : IInscricaoRepository
{
    private readonly UniPlusDbContext _dbContext;
    private readonly IUniPlusEncryptionService _encryption;
    private readonly CalculadorDoIndiceCegoDoCpf _indiceDoCpf;
    private readonly TimeProvider _timeProvider;
    private readonly IComprovanteDaInscricaoRepository _comprovantes;
    private readonly ValorGravadoDaResposta _valores;
    private readonly TraducaoDaInscricao _traducao;
    private readonly SincronizacaoDasLinhasDaInscricao _sincronizacao;

    public InscricaoRepository(
        UniPlusDbContext dbContext,
        IUniPlusEncryptionService encryption,
        CalculadorDoIndiceCegoDoCpf indiceDoCpf,
        TimeProvider timeProvider,
        IComprovanteDaInscricaoRepository comprovantes)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(indiceDoCpf);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(comprovantes);

        _dbContext = dbContext;
        _encryption = encryption;
        _indiceDoCpf = indiceDoCpf;
        _timeProvider = timeProvider;
        _comprovantes = comprovantes;
        _valores = new ValorGravadoDaResposta(encryption);
        _traducao = new TraducaoDaInscricao(_valores);
        _sincronizacao = new SincronizacaoDasLinhasDaInscricao(dbContext);
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

    public Task<VinculoDaInscricao?> ObterVinculoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Inscricoes
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(static r => new VinculoDaInscricao(
                r.Id, r.ProcessoSeletivoId, r.Situacao, r.VersaoFormularioId, r.OfertaCursoId, r.OfertaCursoSegundaOpcaoId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<RespostaIdentificadora?> LocalizarIdentificadorAsync(
        Guid inscricaoId, string fatoCodigo, string? grupoCodigo, string? ocorrenciaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);

        if (grupoCodigo is null)
        {
            return await RespostaIdentificadoraAsync(inscricaoId, fatoCodigo, null, cancellationToken).ConfigureAwait(false);
        }

        Guid? ocorrencia = ocorrenciaId is null
            ? null
            : await OcorrenciaDoGrupoAsync(inscricaoId, grupoCodigo, ocorrenciaId, cancellationToken).ConfigureAwait(false);

        return ocorrencia is null
            ? null
            : await RespostaIdentificadoraAsync(inscricaoId, fatoCodigo, ocorrencia, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> LerIdentificadorAsync(Guid respostaId, CancellationToken cancellationToken = default)
    {
        byte[] cifrado = await ValorCifradoDaRespostaAsync(respostaId, cancellationToken).ConfigureAwait(false);
        JsonElement valor = await _valores
            .DecifrarAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, cifrado, cancellationToken)
            .ConfigureAwait(false);

        return valor.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(valor.GetString()) ? null : valor.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => valor.GetRawText(),
        };
    }

    public async Task<(IReadOnlyList<ResumoDaInscricaoDoProcesso> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarEnviadasDoProcessoAsync(
        Guid processoSeletivoId,
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        bool lerNome,
        bool lerCpf,
        CancellationToken cancellationToken = default)
    {
        IQueryable<InscricaoRecord> enviadas = _dbContext.Inscricoes
            .AsNoTracking()
            .Where(r => r.ProcessoSeletivoId == processoSeletivoId && r.Situacao != SituacaoInscricao.Rascunho);

        CursorKeysetPage<InscricaoRecord> pagina = await CursorKeyset
            .ApplyAsync(enviadas, afterId, limit, direction, cancellationToken)
            .ConfigureAwait(false);

        List<ResumoDaInscricaoDoProcesso> resumos = await ResumosAsync(pagina.Items, lerNome, lerCpf, cancellationToken).ConfigureAwait(false);

        return (resumos, pagina.PrevAfterId, pagina.NextAfterId);
    }

    public async Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = new() { Id = entity.Id, CreatedAt = _timeProvider.GetUtcNow() };
        LinhasDaInscricao desejadas = await _traducao.LinhasAsync(entity, cancellationToken).ConfigureAwait(false);
        byte[] cpfIndice = await ComputarIndiceAsync(entity.Titular.Cpf, cancellationToken).ConfigureAwait(false);
        TraducaoDaInscricao.PreencherRegistro(record, entity, cpfIndice);

        _dbContext.Inscricoes.Add(record);
        _sincronizacao.Sincronizar(LinhasDaInscricao.Vazias, desejadas);
    }

    public async Task AtualizarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = await _dbContext.Inscricoes
            .FirstAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        LinhasDaInscricao desejadas = await _traducao.LinhasAsync(entity, cancellationToken).ConfigureAwait(false);
        byte[] cpfIndice = await ComputarIndiceAsync(entity.Titular.Cpf, cancellationToken).ConfigureAwait(false);
        TraducaoDaInscricao.PreencherRegistro(record, entity, cpfIndice);
        record.UpdatedAt = _timeProvider.GetUtcNow();

        LinhasDaInscricao atuais = await LinhasDaInscricao.CarregarAsync(_dbContext, [entity.Id], cancellationToken).ConfigureAwait(false);
        _sincronizacao.Sincronizar(atuais, desejadas);
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

    private async Task<byte[]> ValorCifradoDaRespostaAsync(Guid respostaId, CancellationToken cancellationToken) =>
        await _dbContext.RespostasDaInscricao
            .AsNoTracking()
            .Where(r => r.Id == respostaId)
            .Select(static r => r.ValorCifrado)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"A resposta identificadora {respostaId} não está cifrada.");

    private async Task<Guid?> OcorrenciaDoGrupoAsync(
        Guid inscricaoId, string grupoCodigo, string membroId, CancellationToken cancellationToken) =>
        await _dbContext.OcorrenciasDeGrupoDaInscricao
            .AsNoTracking()
            .Where(o => o.InscricaoId == inscricaoId && o.GrupoCodigo == grupoCodigo && o.MembroId == membroId)
            .Select(static o => (Guid?)o.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<RespostaIdentificadora?> RespostaIdentificadoraAsync(
        Guid inscricaoId, string fatoCodigo, Guid? ocorrencia, CancellationToken cancellationToken)
    {
        Guid? resposta = await _dbContext.RespostasDaInscricao
            .AsNoTracking()
            .Where(r => r.InscricaoId == inscricaoId
                && r.FatoCodigo == fatoCodigo
                && r.OcorrenciaId == ocorrencia
                && r.Origem == OrigemDaResposta.Informada
                && r.ValorCifrado != null)
            .Select(static r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return resposta is { } respostaId ? new RespostaIdentificadora(respostaId) : null;
    }

    private async Task<List<ResumoDaInscricaoDoProcesso>> ResumosAsync(
        IReadOnlyList<InscricaoRecord> itens, bool lerNome, bool lerCpf, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. itens.Select(static r => r.Id)];

        Dictionary<Guid, string> nomes = await NomesAsync(ids, lerNome, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, DateTimeOffset> primeirosEnvios = await _comprovantes.ObterPrimeirosEnviosAsync(ids, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, string> cpfs = await CpfsAsync(ids, lerCpf, cancellationToken).ConfigureAwait(false);

        return [.. itens.Select(r => ResumoDe(r, nomes, cpfs, primeirosEnvios, lerCpf))];
    }

    private async Task<Dictionary<Guid, string>> NomesAsync(Guid[] ids, bool lerNome, CancellationToken cancellationToken) =>
        lerNome
            ? await NomeDeExibicaoDaInscricao.LerAsync(_dbContext.RespostasDaInscricao, ids, cancellationToken).ConfigureAwait(false)
            : [];

    private async Task<Dictionary<Guid, string>> CpfsAsync(Guid[] ids, bool lerCpf, CancellationToken cancellationToken) =>
        lerCpf
            ? await CpfDaInscricao
                .LerAsync(_dbContext.RespostasDaInscricao.Where(r => ids.Contains(r.InscricaoId)), _encryption, cancellationToken)
                .ConfigureAwait(false)
            : [];

    private static ResumoDaInscricaoDoProcesso ResumoDe(
        InscricaoRecord record,
        Dictionary<Guid, string> nomes,
        Dictionary<Guid, string> cpfs,
        IReadOnlyDictionary<Guid, DateTimeOffset> primeirosEnvios,
        bool lerCpf) =>
        new(
            record.Id,
            record.NumeroInscricao,
            record.Situacao,
            record.VersaoFormularioId,
            record.OfertaCursoId,
            record.OfertaCursoSegundaOpcaoId,
            nomes.GetValueOrDefault(record.Id),
            lerCpf ? CpfDaInscricao.Revalidar(CpfDaInscricao.Exigir(cpfs, record.Id)) : null,
            primeirosEnvios.TryGetValue(record.Id, out DateTimeOffset primeiro) ? primeiro : null);

    private async Task<List<Inscricao>> ParaDominioAsync(List<InscricaoRecord> records, CancellationToken cancellationToken)
    {
        LinhasDaInscricao linhas = await LinhasDaInscricao
            .CarregarAsync(_dbContext, [.. records.Select(static r => r.Id)], cancellationToken)
            .ConfigureAwait(false);

        List<Inscricao> inscricoes = new(records.Count);
        foreach (InscricaoRecord record in records)
        {
            inscricoes.Add(await _traducao.InscricaoAsync(record, linhas.Da(record.Id), cancellationToken).ConfigureAwait(false));
        }

        return inscricoes;
    }

    private Task<byte[]> ComputarIndiceAsync(Cpf cpf, CancellationToken cancellationToken) =>
        _indiceDoCpf.ComputarAsync(cpf.Valor, cancellationToken);
}
