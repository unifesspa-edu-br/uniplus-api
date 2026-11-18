namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// Leva as linhas atuais da inscrição às desejadas pela chave natural: a ocorrência casa pelo grupo e pelo membro, a resposta
/// pela ocorrência, pelo fato e pela posição, o endereço pela ocorrência e pelo fato, o aceite pela ordem. Linha que casa
/// continua a mesma e só recebe o conteúdo novo; assim nenhuma colide com o índice único dentro da mesma gravação.
/// </summary>
internal sealed class SincronizacaoDasLinhasDaInscricao
{
    private readonly UniPlusDbContext _dbContext;

    public SincronizacaoDasLinhasDaInscricao(UniPlusDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    public void Sincronizar(LinhasDaInscricao atuais, LinhasDaInscricao desejadas)
    {
        Dictionary<(string, string), OcorrenciaDeGrupoDaInscricaoRecord> ocorrenciasSobrantes =
            atuais.Ocorrencias.ToDictionary(static o => (o.GrupoCodigo, o.MembroId));
        Dictionary<Guid, Guid> idDaOcorrencia = ReaproveitarOcorrencias(desejadas.Ocorrencias, ocorrenciasSobrantes);

        ReligarAsOcorrencias(desejadas, idDaOcorrencia);
        SincronizarRespostas(atuais, desejadas);
        SincronizarEnderecos(atuais, desejadas);
        SincronizarAceites(atuais, desejadas);
        _dbContext.OcorrenciasDeGrupoDaInscricao.RemoveRange(ocorrenciasSobrantes.Values);
    }

    private Dictionary<Guid, Guid> ReaproveitarOcorrencias(
        IReadOnlyList<OcorrenciaDeGrupoDaInscricaoRecord> desejadas,
        Dictionary<(string, string), OcorrenciaDeGrupoDaInscricaoRecord> ocorrenciasSobrantes)
    {
        Dictionary<Guid, Guid> idDaOcorrencia = [];
        foreach (OcorrenciaDeGrupoDaInscricaoRecord desejada in desejadas)
        {
            idDaOcorrencia[desejada.Id] = OcorrenciaQueFica(desejada, ocorrenciasSobrantes).Id;
        }

        return idDaOcorrencia;
    }

    private OcorrenciaDeGrupoDaInscricaoRecord OcorrenciaQueFica(
        OcorrenciaDeGrupoDaInscricaoRecord desejada,
        Dictionary<(string, string), OcorrenciaDeGrupoDaInscricaoRecord> ocorrenciasSobrantes)
    {
        if (ocorrenciasSobrantes.Remove((desejada.GrupoCodigo, desejada.MembroId), out OcorrenciaDeGrupoDaInscricaoRecord? atual))
        {
            atual.Ordem = desejada.Ordem;
            return atual;
        }

        _dbContext.OcorrenciasDeGrupoDaInscricao.Add(desejada);
        return desejada;
    }

    private static void ReligarAsOcorrencias(LinhasDaInscricao desejadas, Dictionary<Guid, Guid> idDaOcorrencia)
    {
        foreach (RespostaDaInscricaoRecord resposta in desejadas.Respostas.Where(static r => r.OcorrenciaId is not null))
        {
            resposta.OcorrenciaId = idDaOcorrencia[resposta.OcorrenciaId!.Value];
        }

        foreach (EnderecoDaInscricaoRecord endereco in desejadas.Enderecos.Where(static e => e.OcorrenciaId is not null))
        {
            endereco.OcorrenciaId = idDaOcorrencia[endereco.OcorrenciaId!.Value];
        }
    }

    private void SincronizarRespostas(LinhasDaInscricao atuais, LinhasDaInscricao desejadas) =>
        SincronizarPorChave(
            _dbContext.RespostasDaInscricao,
            atuais.Respostas,
            desejadas.Respostas,
            static r => (r.OcorrenciaId, r.FatoCodigo, r.Posicao),
            ValorGravadoDaResposta.CopiarConteudo);

    private void SincronizarEnderecos(LinhasDaInscricao atuais, LinhasDaInscricao desejadas) =>
        SincronizarPorChave(
            _dbContext.EnderecosDaInscricao,
            atuais.Enderecos,
            desejadas.Enderecos,
            static e => (e.OcorrenciaId, e.FatoCodigo),
            EnderecoGravadoDaInscricao.CopiarConteudo);

    private void SincronizarAceites(LinhasDaInscricao atuais, LinhasDaInscricao desejadas) =>
        SincronizarPorChave(
            _dbContext.AceitesDaInscricao,
            atuais.Aceites,
            desejadas.Aceites,
            static a => a.Ordem,
            CopiarAceite);

    private static void CopiarAceite(AceiteDaInscricaoRecord de, AceiteDaInscricaoRecord para)
    {
        para.TermoCodigo = de.TermoCodigo;
        para.VersaoId = de.VersaoId;
        para.HashVersao = de.HashVersao;
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
}
