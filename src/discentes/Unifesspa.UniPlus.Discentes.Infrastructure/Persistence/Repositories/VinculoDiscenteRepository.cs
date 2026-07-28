namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Protege o CPF na fronteira do repositório (ADR-0121, Opção C): o EF Core só
/// materializa <see cref="VinculoDiscenteRecord"/> (envelope cifrado); a tradução
/// para/de <see cref="VinculoDiscente"/> — incluindo cifrar/decifrar via
/// <see cref="IUniPlusEncryptionService"/>, assíncrono — acontece inteiramente aqui,
/// nunca num <c>ValueConverter</c> síncrono.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em DiscentesInfrastructureRegistration.")]
public sealed class VinculoDiscenteRepository : IVinculoDiscenteRepository
{
    private readonly DiscentesDbContext _dbContext;
    private readonly IUniPlusEncryptionService _encryption;

    public VinculoDiscenteRepository(DiscentesDbContext dbContext, IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(encryption);

        _dbContext = dbContext;
        _encryption = encryption;
    }

    public async Task<VinculoDiscente?> ObterVinculoDiscenteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        VinculoDiscenteRecord? record = await _dbContext.VinculosDiscentes
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VinculoDiscente?> ObterComIdSigaaAsync(long idDiscenteSigaa, CancellationToken cancellationToken = default)
    {
        VinculoDiscenteRecord? record = await _dbContext.VinculosDiscentes
            .FirstOrDefaultAsync(r => r.IdDiscenteSigaa == idDiscenteSigaa, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarVinculoDiscenteAsync(VinculoDiscente entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        VinculoDiscenteRecord record = await ParaRegistroNovoAsync(entity, cancellationToken).ConfigureAwait(false);
        await _dbContext.VinculosDiscentes.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task AtualizarVinculoDiscenteAsync(VinculoDiscente entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        VinculoDiscenteRecord record = await _dbContext.VinculosDiscentes
            .FirstAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
    }

    private async Task<VinculoDiscente> ParaDominioAsync(VinculoDiscenteRecord record, CancellationToken cancellationToken)
    {
        byte[] cpfPlano = await _encryption
            .DecryptAsync(DiscentesEncryptionKeys.IdentificadoresPessoais, record.CpfCiphertext, cancellationToken)
            .ConfigureAwait(false);

        Cpf cpf = ReidratarCpf(cpfPlano);

        return new VinculoDiscente(
            record.Id,
            record.IdDiscenteSigaa,
            record.Matricula,
            cpf,
            record.Nome,
            record.Nivel,
            record.CursoId,
            record.CursoNome,
            record.CursoCodigoEmec,
            record.CursoUnidadeId,
            record.CursoUnidadeNome,
            record.SituacaoId,
            record.SituacaoDescricao,
            record.SituacaoVinculo,
            record.AnoIngresso,
            record.PeriodoIngresso);
    }

    private async Task<VinculoDiscenteRecord> ParaRegistroNovoAsync(VinculoDiscente entity, CancellationToken cancellationToken)
    {
        VinculoDiscenteRecord record = new() { Id = entity.Id };
        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        return record;
    }

    private async Task AtualizarCamposAsync(VinculoDiscenteRecord record, VinculoDiscente entity, CancellationToken cancellationToken)
    {
        // Cifra ANTES de mutar qualquer campo do record rastreado pelo ChangeTracker.
        // Se a cifragem falhar (ex.: Vault indisponível durante um sync em lote que
        // continua para as próximas linhas após capturar a exceção), este record
        // permanece intocado — nunca fica com IdDiscenteSigaa/Matricula novos e o
        // CpfCiphertext antigo, uma mutação parcial que um SaveChangesAsync posterior
        // (das linhas que tiveram sucesso) persistiria por engano.
        byte[] cpfCiphertext = await CifrarCpfAsync(entity.Cpf, cancellationToken).ConfigureAwait(false);

        record.IdDiscenteSigaa = entity.IdDiscenteSigaa;
        record.Matricula = entity.Matricula;
        record.CpfCiphertext = cpfCiphertext;
        record.Nome = entity.Nome;
        record.Nivel = entity.Nivel;
        record.CursoId = entity.CursoId;
        record.CursoNome = entity.CursoNome;
        record.CursoCodigoEmec = entity.CursoCodigoEmec;
        record.CursoUnidadeId = entity.CursoUnidadeId;
        record.CursoUnidadeNome = entity.CursoUnidadeNome;
        record.SituacaoId = entity.SituacaoId;
        record.SituacaoDescricao = entity.SituacaoDescricao;
        record.SituacaoVinculo = entity.SituacaoVinculo;
        record.AnoIngresso = entity.AnoIngresso;
        record.PeriodoIngresso = entity.PeriodoIngresso;
    }

    private Task<byte[]> CifrarCpfAsync(Cpf cpf, CancellationToken cancellationToken) =>
        _encryption.EncryptAsync(
            DiscentesEncryptionKeys.IdentificadoresPessoais,
            Encoding.UTF8.GetBytes(cpf.Valor),
            cancellationToken);

    /// <summary>
    /// Revalida o CPF decifrado contra a regra de domínio — dado corrompido no banco
    /// ou chave de cifragem incorreta falha alto, em vez de produzir um <see cref="Cpf"/>
    /// inválido silenciosamente (mesmo espírito do <c>CpfValueConverter</c> atual).
    /// </summary>
    private static Cpf ReidratarCpf(byte[] cpfPlano)
    {
        string valor = Encoding.UTF8.GetString(cpfPlano);

        return Cpf.Criar(valor).Match(
            cpf => cpf,
            erro => throw new InvalidOperationException(
                $"CPF decifrado da tabela vinculo_discente falhou a revalidação de domínio ({erro.Code})."));
    }
}
