namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Traduz o vínculo entre o registro persistido e o domínio. A cifra do CPF fica com o
/// repositório: aqui o CPF já chega cifrado, ou já decifrado.
/// </summary>
internal static class MapeamentoDoVinculoDiscente
{
    /// <summary>
    /// Copia o estado do vínculo para o registro.
    /// </summary>
    public static void Aplicar(VinculoDiscenteRecord record, VinculoDiscenteSnapshot snapshot, byte[] cpfCiphertext)
    {
        record.CpfCiphertext = cpfCiphertext;

        AplicarIdentificacao(record, snapshot);
        AplicarCurso(record, snapshot.Curso);
        AplicarSituacao(record, snapshot.Situacao);
        AplicarIngresso(record, snapshot.Ingresso);
    }

    /// <summary>
    /// Reconstrói o vínculo a partir do registro. Dado persistido que não passa mais pela
    /// regra de domínio falha alto, em vez de produzir um vínculo inválido silenciosamente.
    /// </summary>
    public static VinculoDiscente ParaDominio(VinculoDiscenteRecord record, Cpf cpf)
    {
        CursoSigaaSnapshot curso = CursoDe(record);
        SituacaoAcademicaSnapshot situacao = SituacaoDe(record);
        PeriodoIngresso ingresso = IngressoDe(record);
        IdentificacaoDoDiscente identificacao = IdentificacaoDe(record, cpf);

        VinculoDiscenteSnapshot snapshot = Exigir(
            VinculoDiscenteSnapshot.Criar(identificacao, record.Nivel, curso, situacao, ingresso),
            "Snapshot corrompido");

        return VinculoDiscente.Reidratar(record.Id, snapshot);
    }

    private static void AplicarIdentificacao(VinculoDiscenteRecord record, VinculoDiscenteSnapshot snapshot)
    {
        record.IdDiscenteSigaa = snapshot.Identificacao.IdDiscenteSigaa;
        record.Matricula = snapshot.Identificacao.Matricula;
        record.Nome = snapshot.Identificacao.Nome;
        record.Nivel = snapshot.Nivel;
    }

    private static void AplicarCurso(VinculoDiscenteRecord record, CursoSigaaSnapshot curso)
    {
        record.CursoId = curso.Id;
        record.CursoNome = curso.Nome;
        record.CursoCodigoEmec = curso.CodigoEmec;
        record.CursoUnidadeId = curso.UnidadeId;
        record.CursoUnidadeNome = curso.UnidadeNome;
    }

    private static void AplicarSituacao(VinculoDiscenteRecord record, SituacaoAcademicaSnapshot situacao)
    {
        record.SituacaoId = situacao.Id;
        record.SituacaoDescricao = situacao.Descricao;
        record.SituacaoVinculo = situacao.Vinculo;
    }

    private static void AplicarIngresso(VinculoDiscenteRecord record, PeriodoIngresso ingresso)
    {
        record.AnoIngresso = ingresso.Ano;
        record.PeriodoIngresso = ingresso.Periodo;
    }

    private static IdentificacaoDoDiscente IdentificacaoDe(VinculoDiscenteRecord record, Cpf cpf) =>
        Exigir(
            IdentificacaoDoDiscente.Criar(record.IdDiscenteSigaa, record.Matricula, cpf, record.Nome),
            "Snapshot corrompido");

    private static CursoSigaaSnapshot CursoDe(VinculoDiscenteRecord record) =>
        Exigir(
            CursoSigaaSnapshot.Criar(
                record.CursoId, record.CursoNome, record.CursoCodigoEmec, record.CursoUnidadeId, record.CursoUnidadeNome),
            "Curso corrompido");

    private static SituacaoAcademicaSnapshot SituacaoDe(VinculoDiscenteRecord record) =>
        Exigir(
            SituacaoAcademicaSnapshot.Criar(record.SituacaoId, record.SituacaoDescricao, record.SituacaoVinculo),
            "Situação corrompida");

    private static PeriodoIngresso IngressoDe(VinculoDiscenteRecord record) =>
        Exigir(PeriodoIngresso.Criar(record.AnoIngresso, record.PeriodoIngresso), "Período corrompido");

    private static T Exigir<T>(Result<T> resultado, string descricaoDoDefeito) =>
        resultado.Match(
            valor => valor,
            erro => throw new InvalidOperationException(
                $"{descricaoDoDefeito} no registro persistido ({erro.Code})."));
}
