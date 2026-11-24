namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Os motivos do cadastro e as escolhas do processo que os testes dos handlers de motivo usam.</summary>
internal static class MotivoDeTeste
{
    internal static MotivoDeDecisao Motivo(
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisao.IsencaoDaTaxa, string codigo = "RENDA_ACIMA_DO_LIMITE", string? subclassificacao = "CADASTRO_UNICO") =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(tipo.Codigo(), codigo, "Renda familiar per capita acima do limite legal.", subclassificacao, SentidoDoMotivo.Desfavoravel)).Value!;

    /// <summary>O lugar da primeira fase do cronograma do processo.</summary>
    internal static LugarDaDecisao LugarDaPrimeiraFase(ProcessoSeletivo processo) =>
        LugarDaDecisao.DaFase(processo.CronogramaFases.First().FaseCanonicaOrigemId);

    /// <summary>A escolha do motivo no lugar, incluída pelo domínio no processo em rascunho.</summary>
    internal static MotivoDeDecisaoDoProcesso Escolhido(ProcessoSeletivo processo, LugarDaDecisao lugar, MotivoDeDecisao origem) =>
        EscolhaDosMotivos.Incluir(new InclusaoDeMotivo(processo, lugar, [], origem)).Value!;

    /// <summary>A porta dos motivos escolhidos de um processo que ainda não escolheu nenhum.</summary>
    internal static IMotivoDeDecisaoDoProcessoRepository SemEscolha()
    {
        IMotivoDeDecisaoDoProcessoRepository repository = Substitute.For<IMotivoDeDecisaoDoProcessoRepository>();
        repository.ListarEscolhaAtualAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        repository.ListarEscolhaAtualComOrigemAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        return repository;
    }
}
