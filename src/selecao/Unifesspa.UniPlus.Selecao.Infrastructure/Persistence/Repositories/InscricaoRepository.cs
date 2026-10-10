namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Protege o CPF e os demais números de documento na fronteira do repositório (ADR-0121, Opção
/// C): o EF Core só materializa <see cref="InscricaoRecord"/>; a tradução para/de
/// <see cref="Inscricao"/> — incluindo cifrar/decifrar via <see cref="IUniPlusEncryptionService"/> e
/// computar o índice cego via <see cref="IUniPlusBlindIndexService"/>, ambos assíncronos —
/// acontece inteiramente aqui, nunca num <c>ValueConverter</c> síncrono.
/// </summary>
/// <remarks>
/// <see cref="DocumentosDeclarados"/> é o envelope que separa os números de documento do jsonb em
/// claro: <see cref="DocumentosDeclarados.Respostas"/> para os de nível superior, e
/// <see cref="DocumentosDeclarados.Grupos"/> para os de uma ocorrência de grupo repetível —
/// chaveado por <c>(código do grupo, id do membro)</c>, nunca pela posição, e separado dos de
/// nível superior porque o mesmo código de fato pode existir nos dois níveis sem colidir.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em SelecaoInfrastructureRegistration.")]
public sealed class InscricaoRepository : IInscricaoRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

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

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Inscricao?> ObterPorProcessoECpfAsync(
        Guid processoSeletivoId, Cpf cpf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        byte[] indice = await ComputarIndiceAsync(cpf, cancellationToken).ConfigureAwait(false);

        InscricaoRecord? record = await _dbContext.Inscricoes
            .FirstOrDefaultAsync(
                r => r.ProcessoSeletivoId == processoSeletivoId && r.CpfIndice.SequenceEqual(indice),
                cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = new() { Id = entity.Id, CreatedAt = _timeProvider.GetUtcNow() };
        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        _dbContext.Inscricoes.Add(record);
    }

    public async Task AtualizarAsync(Inscricao entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        InscricaoRecord record = await _dbContext.Inscricoes
            .FirstAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        record.UpdatedAt = _timeProvider.GetUtcNow();
    }

    public Task AdicionarComprovanteAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comprovante);

        _dbContext.ComprovantesInscricao.Add(comprovante);
        return Task.CompletedTask;
    }

    private async Task<Inscricao> ParaDominioAsync(InscricaoRecord record, CancellationToken cancellationToken)
    {
        byte[] cpfPlano = await _encryption
            .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, record.CpfCiphertext, cancellationToken)
            .ConfigureAwait(false);
        Cpf cpf = ReidratarCpf(cpfPlano);

        byte[] documentosPlano = await _encryption
            .DecryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, record.DocumentosCifrados, cancellationToken)
            .ConfigureAwait(false);
        DocumentosDeclarados documentos = JsonSerializer.Deserialize<DocumentosDeclarados>(documentosPlano, JsonOptions)!;

        Dictionary<string, JsonElement> respostas = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(record.RespostasJson, JsonOptions)!;
        foreach ((string codigo, JsonElement valor) in documentos.Respostas)
        {
            respostas[codigo] = valor;
        }

        Dictionary<string, List<OcorrenciaDeGrupoInscricao>> gruposClaros =
            JsonSerializer.Deserialize<Dictionary<string, List<OcorrenciaDeGrupoInscricao>>>(record.GruposJson, JsonOptions)!;
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(StringComparer.Ordinal);
        foreach ((string grupoCodigo, List<OcorrenciaDeGrupoInscricao> ocorrencias) in gruposClaros)
        {
            Dictionary<string, Dictionary<string, JsonElement>> documentosDoGrupo =
                documentos.Grupos.TryGetValue(grupoCodigo, out Dictionary<string, Dictionary<string, JsonElement>>? porMembro)
                    ? porMembro
                    : new(StringComparer.Ordinal);

            grupos[grupoCodigo] = [.. ocorrencias.Select(o => ComDocumentosDoMembro(o, documentosDoGrupo))];
        }

        List<AceiteDeTermo> aceites = JsonSerializer.Deserialize<List<AceiteDeTermo>>(record.AceitesJson, JsonOptions)!;
        HashSet<string> fatosIdentificador = new(
            JsonSerializer.Deserialize<List<string>>(record.FatosIdentificadorDeclaradosJson, JsonOptions)!,
            StringComparer.Ordinal);

        return Inscricao.Reidratar(
            record.Id, record.ProcessoSeletivoId, cpf, record.Sub, record.EmissorToken, record.Situacao,
            record.VersaoFormularioId, record.OfertaCursoId, record.NumeroInscricao,
            respostas, grupos, aceites, fatosIdentificador,
            record.Revisao, record.CreatedAt, record.UpdatedAt);
    }

    private static OcorrenciaDeGrupoInscricao ComDocumentosDoMembro(
        OcorrenciaDeGrupoInscricao ocorrencia, Dictionary<string, Dictionary<string, JsonElement>> documentosDoGrupo)
    {
        if (!documentosDoGrupo.TryGetValue(ocorrencia.Id, out Dictionary<string, JsonElement>? documentosDoMembro))
        {
            return ocorrencia;
        }

        Dictionary<string, JsonElement> mescladas = new(ocorrencia.Respostas, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in documentosDoMembro)
        {
            mescladas[codigo] = valor;
        }

        return ocorrencia with { Respostas = mescladas };
    }

    /// <summary>
    /// Cifra ANTES de mutar qualquer campo do record rastreado pelo ChangeTracker. Se a cifragem
    /// falhar, este record permanece intocado — nunca fica com colunas novas e o envelope antigo,
    /// uma mutação parcial que um <c>SaveChangesAsync</c> persistiria por engano.
    /// </summary>
    /// <remarks>
    /// Não toca <c>CreatedAt</c>/<c>UpdatedAt</c>: o <c>AuditableInterceptor</c> carimba toda
    /// <see cref="Kernel.Domain.Entities.EntityBase"/> rastreada, mas quem o EF rastreia aqui é
    /// <see cref="InscricaoRecord"/>, que não herda <c>EntityBase</c> — o carimbo é
    /// responsabilidade explícita de quem chama (<see cref="AdicionarAsync"/>/
    /// <see cref="AtualizarAsync"/>), via <see cref="_timeProvider"/>.
    /// </remarks>
    private async Task AtualizarCamposAsync(InscricaoRecord record, Inscricao entity, CancellationToken cancellationToken)
    {
        byte[] cpfCiphertext = await _encryption
            .EncryptAsync(SelecaoEncryptionKeys.IdentificadoresAesGcm, Encoding.UTF8.GetBytes(entity.Cpf.Valor), cancellationToken)
            .ConfigureAwait(false);
        byte[] cpfIndice = await ComputarIndiceAsync(entity.Cpf, cancellationToken).ConfigureAwait(false);

        (Dictionary<string, JsonElement> respostasClaras, Dictionary<string, JsonElement> documentosDeRespostas) =
            Separar(entity.Respostas, entity.FatosIdentificadorDeclarados);

        Dictionary<string, List<OcorrenciaDeGrupoInscricao>> gruposClaros = new(StringComparer.Ordinal);
        Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> documentosDeGrupos = new(StringComparer.Ordinal);
        foreach ((string grupoCodigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in entity.Grupos)
        {
            List<OcorrenciaDeGrupoInscricao> ocorrenciasClaras = [];
            Dictionary<string, Dictionary<string, JsonElement>> documentosPorMembro = new(StringComparer.Ordinal);
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                (Dictionary<string, JsonElement> claras, Dictionary<string, JsonElement> documentosDoMembro) =
                    Separar(ocorrencia.Respostas, entity.FatosIdentificadorDeclarados);
                ocorrenciasClaras.Add(ocorrencia with { Respostas = claras });
                if (documentosDoMembro.Count > 0)
                {
                    documentosPorMembro[ocorrencia.Id] = documentosDoMembro;
                }
            }

            gruposClaros[grupoCodigo] = ocorrenciasClaras;
            if (documentosPorMembro.Count > 0)
            {
                documentosDeGrupos[grupoCodigo] = documentosPorMembro;
            }
        }

        byte[] documentosCifrados = await _encryption
            .EncryptAsync(
                SelecaoEncryptionKeys.IdentificadoresAesGcm,
                JsonSerializer.SerializeToUtf8Bytes(new DocumentosDeclarados(documentosDeRespostas, documentosDeGrupos), JsonOptions),
                cancellationToken)
            .ConfigureAwait(false);

        record.ProcessoSeletivoId = entity.ProcessoSeletivoId;
        record.CpfCiphertext = cpfCiphertext;
        record.CpfIndice = cpfIndice;
        record.Sub = entity.Sub;
        record.EmissorToken = entity.EmissorToken;
        record.Situacao = entity.Situacao;
        record.VersaoFormularioId = entity.VersaoFormularioId;
        record.OfertaCursoId = entity.OfertaCursoId;
        record.NumeroInscricao = entity.NumeroInscricao;
        record.RespostasJson = JsonSerializer.Serialize(respostasClaras, JsonOptions);
        record.GruposJson = JsonSerializer.Serialize(gruposClaros, JsonOptions);
        record.AceitesJson = JsonSerializer.Serialize(entity.Aceites, JsonOptions);
        record.FatosIdentificadorDeclaradosJson = JsonSerializer.Serialize(entity.FatosIdentificadorDeclarados, JsonOptions);
        record.DocumentosCifrados = documentosCifrados;
        record.Revisao = entity.Revisao;
    }

    private static (Dictionary<string, JsonElement> Claras, Dictionary<string, JsonElement> Documentos) Separar(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlySet<string> fatosIdentificador)
    {
        Dictionary<string, JsonElement> claras = new(StringComparer.Ordinal);
        Dictionary<string, JsonElement> documentos = new(StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            if (fatosIdentificador.Contains(codigo))
            {
                documentos[codigo] = valor;
            }
            else
            {
                claras[codigo] = valor;
            }
        }

        return (claras, documentos);
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

    /// <summary><see cref="Grupos"/> é chaveado por (código do grupo, id do membro) → mapa de documentos do membro.</summary>
    private sealed record DocumentosDeclarados(
        Dictionary<string, JsonElement> Respostas,
        Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>> Grupos);
}
