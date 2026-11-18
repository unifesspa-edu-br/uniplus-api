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

        VinculoDiscenteCopiado dadosCopiados = Exigir(
            VinculoDiscenteCopiado.Criar(identificacao, record.Nivel, curso, situacao, ingresso),
            "Dados copiados corrompidos");

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
        Exigir(
            IdentificacaoDoDiscente.Criar(record.IdDiscenteSigaa, record.Matricula, cpf, record.Nome),
            "Dados copiados corrompidos");

    private static CursoCopiadoDoSigaa CursoDe(VinculoDiscenteRecord record) =>
        Exigir(
            CursoCopiadoDoSigaa.Criar(
                record.CursoId, record.CursoNome, record.CursoCodigoEmec, record.CursoUnidadeId, record.CursoUnidadeNome),
            "Curso corrompido");

    private static SituacaoAcademicaCopiada SituacaoDe(VinculoDiscenteRecord record) =>
        Exigir(
            SituacaoAcademicaCopiada.Criar(record.SituacaoId, record.SituacaoDescricao, record.SituacaoVinculo),
            "Situação corrompida");

    private static PeriodoIngresso IngressoDe(VinculoDiscenteRecord record) =>
        Exigir(PeriodoIngresso.Criar(record.AnoIngresso, record.PeriodoIngresso), "Período corrompido");

    private static T Exigir<T>(Result<T> resultado, string descricaoDoDefeito) =>
        resultado.Match(
            valor => valor,
            erro => throw new InvalidOperationException(
                $"{descricaoDoDefeito} no registro persistido ({erro.Code})."));
}
