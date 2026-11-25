namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

using System.Linq;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Cryptography;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Protege o CPF na fronteira do repositório (ADR-0121, Opção C): o EF Core só
/// materializa <see cref="VinculoDiscenteRecord"/> (envelope cifrado); a tradução
/// para/de <see cref="VinculoDiscente"/> — incluindo cifrar/decifrar via
/// <see cref="IUniPlusEncryptionService"/>, assíncrono — acontece inteiramente aqui,
/// nunca num <c>ValueConverter</c> síncrono.
/// </summary>
public sealed class VinculoDiscenteRepository : IVinculoDiscenteRepository
{
    private readonly UniPlusDbContext _dbContext;
    private readonly IUniPlusEncryptionService _encryption;

    public VinculoDiscenteRepository(UniPlusDbContext dbContext, IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(encryption);

        _dbContext = dbContext;
        _encryption = encryption;
    }

    public async Task<VinculoDiscente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        VinculoDiscenteRecord? record = await _dbContext.VinculosDiscentes
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VinculoDiscente?> ObterPorIdSigaaAsync(IdDoDiscenteNoSigaa idDiscenteSigaa, CancellationToken cancellationToken = default)
    {
        VinculoDiscenteRecord? record = await _dbContext.VinculosDiscentes
            .FirstOrDefaultAsync(r => r.IdDiscenteSigaa == idDiscenteSigaa.Valor, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : await ParaDominioAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdicionarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        VinculoDiscenteRecord record = await ParaRegistroNovoAsync(entity, cancellationToken).ConfigureAwait(false);
        _dbContext.VinculosDiscentes.Add(record);
    }

    /// <summary>
    /// Atualiza um vínculo fora da sincronização.
    /// </summary>
    /// <remarks>
    /// Limpa o resumo do conteúdo, porque ele deixou de descrever o que está guardado. Sem
    /// isso, a próxima sincronização compararia o resumo antigo com o da origem, concluiria
    /// que nada mudou, e a alteração feita aqui permaneceria indefinidamente — divergindo
    /// da origem sem que nada apontasse a divergência.
    /// </remarks>
    public async Task AtualizarAsync(VinculoDiscente entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        VinculoDiscenteRecord record = await _dbContext.VinculosDiscentes
            .FirstAsync(r => r.Id == entity.Id, cancellationToken)
            .ConfigureAwait(false);

        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        record.ResumoDoConteudo = string.Empty;
    }

    /// <summary>
    /// Grava um lote da sincronização.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Trabalha no modelo de persistência, e não nas entidades de domínio, por uma razão
    /// que decide o custo da sincronização inteira: o CPF só é decifrado quando alguém pede
    /// o vínculo como domínio. Comparando aqui os registros como estão guardados, uma
    /// execução diária de dezenas de milhares de linhas não decifra nenhum CPF — apenas
    /// cifra os poucos que de fato mudaram.
    /// </para>
    /// <para>
    /// Quem não está no lote não é tocado. Uma execução que só alcançou parte das páginas
    /// não pode apagar o que não chegou a ver.
    /// </para>
    /// </remarks>
    public async Task<ResultadoDaGravacao> GravarLoteAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lote);

        if (lote.Count == 0)
        {
            return new ResultadoDaGravacao(0, 0, 0);
        }

        Dictionary<long, VinculoDiscenteRecord> existentes = await ExistentesDoLoteAsync(lote, cancellationToken)
            .ConfigureAwait(false);

        return await PrepararLoteAsync(lote, existentes, cancellationToken).ConfigureAwait(false);
    }

    private Task<Dictionary<long, VinculoDiscenteRecord>> ExistentesDoLoteAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken)
    {
        long[] identificadores = [.. lote.Select(v => v.Vinculo.DadosCopiados.Identificacao.IdDiscenteSigaa.Valor)];

        return _dbContext.VinculosDiscentes
            .Where(r => identificadores.Contains(r.IdDiscenteSigaa))
            .ToDictionaryAsync(r => r.IdDiscenteSigaa, cancellationToken);
    }

    private async Task<ResultadoDaGravacao> PrepararLoteAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        Dictionary<long, VinculoDiscenteRecord> existentes,
        CancellationToken cancellationToken)
    {
        ResultadoDaGravacao resultado = new(0, 0, 0);

        foreach (VinculoSincronizavel item in lote)
        {
            resultado = await PrepararSomandoAsync(item, existentes, resultado, cancellationToken)
                .ConfigureAwait(false);
        }

        return resultado;
    }

    /// <summary>
    /// Prepara um vínculo do lote e soma o que ele deu ao que o lote já alcançou. Se
    /// preparar falha — cifrar o CPF deste vínculo —, o que já foi classificado até aqui
    /// sobe junto: sem isso, os vínculos reconhecidos como iguais, que continuam corretos na
    /// réplica, seriam contados como não gravados.
    /// </summary>
    private async Task<ResultadoDaGravacao> PrepararSomandoAsync(
        VinculoSincronizavel item,
        Dictionary<long, VinculoDiscenteRecord> existentes,
        ResultadoDaGravacao alcancadoAteAqui,
        CancellationToken cancellationToken)
    {
        try
        {
            ResultadoDaGravacao deste = await PrepararAsync(item, existentes, cancellationToken)
                .ConfigureAwait(false);

            return alcancadoAteAqui.Somar(deste);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            throw new FalhaAoPrepararLoteException(alcancadoAteAqui, excecao);
        }
    }

    private async Task<ResultadoDaGravacao> PrepararAsync(
        VinculoSincronizavel item,
        Dictionary<long, VinculoDiscenteRecord> existentes,
        CancellationToken cancellationToken)
    {
        if (!existentes.TryGetValue(item.Vinculo.DadosCopiados.Identificacao.IdDiscenteSigaa.Valor, out VinculoDiscenteRecord? existente))
        {
            await InserirAsync(item, cancellationToken).ConfigureAwait(false);
            return new ResultadoDaGravacao(1, 0, 0);
        }

        if (string.Equals(existente.ResumoDoConteudo, item.ResumoDoConteudo.Valor, StringComparison.Ordinal))
        {
            return new ResultadoDaGravacao(0, 0, 1);
        }

        await AtualizarDaSincronizacaoAsync(existente, item, cancellationToken).ConfigureAwait(false);
        return new ResultadoDaGravacao(0, 1, 0);
    }

    private async Task InserirAsync(VinculoSincronizavel item, CancellationToken cancellationToken)
    {
        VinculoDiscenteRecord novo = await ParaRegistroNovoAsync(item.Vinculo, cancellationToken).ConfigureAwait(false);

        novo.ResumoDoConteudo = item.ResumoDoConteudo.Valor;
        _dbContext.VinculosDiscentes.Add(novo);
    }

    private async Task AtualizarDaSincronizacaoAsync(
        VinculoDiscenteRecord existente,
        VinculoSincronizavel item,
        CancellationToken cancellationToken)
    {
        await AtualizarCamposAsync(existente, item.Vinculo, cancellationToken).ConfigureAwait(false);
        existente.ResumoDoConteudo = item.ResumoDoConteudo.Valor;
    }

    private async Task<VinculoDiscente> ParaDominioAsync(VinculoDiscenteRecord record, CancellationToken cancellationToken)
    {
        byte[] cpfPlano = await _encryption
            .DecryptAsync(DiscentesEncryptionKeys.IdentificadoresPessoais, record.CpfCiphertext, cancellationToken)
            .ConfigureAwait(false);

        return MapeamentoDoVinculoDiscente.ParaDominio(record, ReidratarCpf(cpfPlano));
    }

    private async Task<VinculoDiscenteRecord> ParaRegistroNovoAsync(VinculoDiscente entity, CancellationToken cancellationToken)
    {
        VinculoDiscenteRecord record = new() { Id = entity.Id };
        await AtualizarCamposAsync(record, entity, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <summary>
    /// Cifra o CPF antes de tocar em qualquer campo do registro rastreado pelo
    /// ChangeTracker. Se a cifragem falhar — Vault indisponível durante uma sincronização em
    /// lote que continua para as próximas linhas depois de capturar a exceção —, o registro
    /// permanece intocado: nunca fica com o identificador e a matrícula novos e o CPF
    /// cifrado antigo, uma mutação parcial que um <c>SaveChangesAsync</c> posterior, das
    /// linhas que tiveram sucesso, persistiria por engano.
    /// </summary>
    private async Task AtualizarCamposAsync(VinculoDiscenteRecord record, VinculoDiscente entity, CancellationToken cancellationToken)
    {
        byte[] cpfCiphertext = await CifrarCpfAsync(entity.DadosCopiados.Identificacao.Cpf, cancellationToken).ConfigureAwait(false);

        MapeamentoDoVinculoDiscente.Aplicar(record, entity.DadosCopiados, cpfCiphertext);
    }

    private Task<byte[]> CifrarCpfAsync(Cpf cpf, CancellationToken cancellationToken) =>
        _encryption.EncryptAsync(
            DiscentesEncryptionKeys.IdentificadoresPessoais,
            Encoding.UTF8.GetBytes(cpf.Valor),
            cancellationToken);

    /// <summary>
    /// Revalida o CPF decifrado contra a regra de domínio — dado corrompido no banco
    /// ou chave de cifragem incorreta falha alto, em vez de produzir um <see cref="Cpf"/>
    /// inválido silenciosamente.
    /// </summary>
    private static Cpf ReidratarCpf(byte[] cpfPlano)
    {
        string valor = Encoding.UTF8.GetString(cpfPlano);

        return Cpf.Criar(valor).Match(
            cpf => cpf,
            erro => throw new InvalidOperationException(
                $"CPF decifrado da tabela vinculos_de_discentes falhou a revalidação de domínio ({erro.Code})."));
    }
}
