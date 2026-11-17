namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os motivos do cadastro e as escolhas do processo que os testes dos handlers de motivo usam.</summary>
internal static class MotivoDeTeste
{
    internal static MotivoDeDecisao Motivo(
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisao.IsencaoDaTaxa, string codigo = "RENDA_ACIMA_DO_LIMITE", string? subclassificacao = FundamentoIsencaoCodigo.CadastroUnico) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo.ToCodigo(), codigo, "Renda familiar per capita acima do limite legal.", subclassificacao, SentidoDoMotivo.Desfavoravel)).Value!;

    /// <summary>O lugar da primeira fase do cronograma do processo.</summary>
    internal static LugarDaDecisao LugarDaPrimeiraFase(ProcessoSeletivo processo) =>
        LugarDaDecisao.DaFase(processo.CronogramaFases.First().FaseCanonicaOrigemId);

    /// <summary>A escolha do motivo no lugar, incluída pelo domínio; a justificativa só vale com o processo publicado.</summary>
    internal static MotivoDoProcesso Escolhido(
        ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem, PrecondicaoIfMatch? precondicao = null) =>
        EscolhaDosMotivos.Incluir(
            new InclusaoDeMotivo(processo, lugar, [], origem, "Retorno do INEP exigido.", precondicao ?? PrecondicaoIfMatch.Ausente, FasesIniciadas.Nenhuma)).Value!;

    /// <summary>A porta dos motivos escolhidos de um processo que ainda não escolheu nenhum.</summary>
    internal static IMotivoDoProcessoRepository SemEscolha()
    {
        IMotivoDoProcessoRepository repository = Substitute.For<IMotivoDoProcessoRepository>();
        repository.ListarEscolhaAtualAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        repository.ListarEscolhaAtualComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        return repository;
    }
}
