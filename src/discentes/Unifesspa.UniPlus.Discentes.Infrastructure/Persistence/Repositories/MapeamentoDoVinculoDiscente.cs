namespace Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.ValueObjects;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Traduz o vínculo entre o registro persistido e o domínio. A cifra do CPF fica com o
/// repositório: aqui o CPF já chega cifrado, ou já decifrado.
/// </summary>
internal static class MapeamentoDoVinculoDiscente
{
    /// <summary>
    /// Copia o estado do vínculo para o registro.
    /// </summary>
    public static void Aplicar(VinculoDiscenteRecord record, VinculoDiscenteCopiado dadosCopiados, byte[] cpfCiphertext)
    {
        record.CpfCiphertext = cpfCiphertext;

        AplicarIdentificacao(record, dadosCopiados);
        AplicarCurso(record, dadosCopiados.Curso);
        AplicarSituacao(record, dadosCopiados.Situacao);
        AplicarIngresso(record, dadosCopiados.Ingresso);
    }

    /// <summary>
    /// Reconstrói o vínculo a partir do registro. Dado persistido que não passa mais pela
    /// regra de domínio falha alto, em vez de produzir um vínculo inválido silenciosamente.
    /// </summary>
    public static VinculoDiscente ParaDominio(VinculoDiscenteRecord record, Cpf cpf)
    {
        CursoCopiadoDoSigaa curso = CursoDe(record);
        SituacaoAcademicaCopiada situacao = SituacaoDe(record);
        PeriodoIngresso ingresso = IngressoDe(record);
        IdentificacaoDoDiscente identificacao = IdentificacaoDe(record, cpf);

        VinculoDiscenteCopiado dadosCopiados = ObjetoDeValorGravado.Reidratar(
            VinculoDiscenteCopiado.Criar(identificacao, record.Nivel, curso, situacao, ingresso),
            nameof(VinculoDiscenteCopiado));

        return VinculoDiscente.Reidratar(record.Id, dadosCopiados);
    }

    private static void AplicarIdentificacao(VinculoDiscenteRecord record, VinculoDiscenteCopiado dadosCopiados)
    {
        record.IdDiscenteSigaa = dadosCopiados.Identificacao.IdDiscenteSigaa.Valor;
        record.Matricula = dadosCopiados.Identificacao.Matricula.Valor;
        record.Nome = dadosCopiados.Identificacao.Nome.Valor;
        record.Nivel = dadosCopiados.Nivel.Valor;
    }

    private static void AplicarCurso(VinculoDiscenteRecord record, CursoCopiadoDoSigaa curso)
    {
        record.CursoId = curso.Id.Valor;
        record.CursoNome = curso.Nome.Valor;
        record.CursoCodigoEmec = curso.CodigoEmec?.Valor;
        record.CursoUnidadeId = curso.Unidade.Id.Valor;
        record.CursoUnidadeNome = curso.Unidade.Nome.Valor;
    }

    private static void AplicarSituacao(VinculoDiscenteRecord record, SituacaoAcademicaCopiada situacao)
    {
        record.SituacaoId = situacao.Id.Valor;
        record.SituacaoDescricao = situacao.Descricao.Valor;
        record.SituacaoVinculo = situacao.Qualificador?.Valor;
    }

    private static void AplicarIngresso(VinculoDiscenteRecord record, PeriodoIngresso ingresso)
    {
        record.AnoIngresso = ingresso.Ano;
        record.PeriodoIngresso = ingresso.Periodo;
    }

    private static IdentificacaoDoDiscente IdentificacaoDe(VinculoDiscenteRecord record, Cpf cpf) =>
        ObjetoDeValorGravado.Reidratar(
            IdentificacaoDoDiscente.Criar(record.IdDiscenteSigaa, record.Matricula, cpf, record.Nome),
            nameof(IdentificacaoDoDiscente));

    private static CursoCopiadoDoSigaa CursoDe(VinculoDiscenteRecord record) =>
        ObjetoDeValorGravado.Reidratar(
            CursoCopiadoDoSigaa.Criar(
                record.CursoId, record.CursoNome, record.CursoCodigoEmec, record.CursoUnidadeId, record.CursoUnidadeNome),
            nameof(CursoCopiadoDoSigaa));

    private static SituacaoAcademicaCopiada SituacaoDe(VinculoDiscenteRecord record) =>
        ObjetoDeValorGravado.Reidratar(
            SituacaoAcademicaCopiada.Criar(record.SituacaoId, record.SituacaoDescricao, record.SituacaoVinculo),
            nameof(SituacaoAcademicaCopiada));

    private static PeriodoIngresso IngressoDe(VinculoDiscenteRecord record) =>
        ObjetoDeValorGravado.Reidratar(PeriodoIngresso.Criar(record.AnoIngresso, record.PeriodoIngresso), nameof(PeriodoIngresso));
}
