namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os pares valor e código dos enums da configuração do recurso, escritos aqui de propósito: alterar um código é alterar o
/// contrato publicado e o dado gravado, e este teste é o que obriga a revisão. O rótulo é texto de tela e não é fixado.
/// </summary>
public sealed class CodigosDeContratoDoRecursoTests
{
    [Fact(DisplayName = "Cada unidade de prazo tem o código que o contrato publica")]
    public void UnidadePrazo_CodigoDeCadaValor() =>
        Enum.GetValues<UnidadePrazo>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<UnidadePrazo, string>
            {
                [UnidadePrazo.Horas] = "HORAS",
                [UnidadePrazo.Dias] = "DIAS",
                [UnidadePrazo.DiasUteis] = "DIAS_UTEIS",
            });

    [Fact(DisplayName = "Cada efeito suspensivo tem o código que o contrato publica")]
    public void EfeitoSuspensivo_CodigoDeCadaValor() =>
        Enum.GetValues<EfeitoSuspensivo>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<EfeitoSuspensivo, string>
            {
                [EfeitoSuspensivo.ParaTodosOsRecursos] = "PARA_TODOS_OS_RECURSOS",
                [EfeitoSuspensivo.SemEfeitoSuspensivo] = "SEM_EFEITO_SUSPENSIVO",
                [EfeitoSuspensivo.UnidadeDecideEmCadaRecurso] = "UNIDADE_DECIDE_EM_CADA_RECURSO",
            });

    [Fact(DisplayName = "Cada guarda da vaga tem o código que o contrato publica")]
    public void GuardaDaVaga_CodigoDeCadaValor() =>
        Enum.GetValues<GuardaDaVaga>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<GuardaDaVaga, string>
            {
                [GuardaDaVaga.AteOResultadoDefinitivo] = "ATE_O_RESULTADO_DEFINITIVO",
                [GuardaDaVaga.NaoGuarda] = "NAO_GUARDA",
            });

    [Fact(DisplayName = "Cada tipo de alvo tem o código gravado no contrato")]
    public void TipoDeAlvo_CodigoDeCadaValor() =>
        Enum.GetValues<TipoDeAlvoDoProcedimentoDeRecurso>().ToDictionary(static v => v, static v => v.Codigo())
            .Should().Equal(new Dictionary<TipoDeAlvoDoProcedimentoDeRecurso, string>
            {
                [TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao] = "ITEM_AVALIACAO",
                [TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem] = "AREA_ENEM",
            });

    [Fact(DisplayName = "Todo valor dos enums do recurso tem rótulo")]
    public void Rotulo_DeCadaValor_NaoEstaVazio()
    {
        Enum.GetValues<UnidadePrazo>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));
        Enum.GetValues<EfeitoSuspensivo>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));
        Enum.GetValues<GuardaDaVaga>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Rotulo()));
    }

    [Fact(DisplayName = "Todo efeito suspensivo e toda guarda da vaga declaram a descrição que a leitura de opções exige")]
    public void Descricao_DeCadaOpcaoDoEfeitoEDaGuarda_EstaPreenchida()
    {
        Enum.GetValues<EfeitoSuspensivo>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Descricao()));
        Enum.GetValues<GuardaDaVaga>().Should().OnlyContain(static v => !string.IsNullOrWhiteSpace(v.Descricao()));
    }

    [Fact(DisplayName = "As descrições das opções são as que o operador lê, sem citar código nem termo interno")]
    public void Descricao_DeCadaOpcao_EhOTextoConfirmado()
    {
        EfeitoSuspensivo.ParaTodosOsRecursos.Descricao().Should().Be(
            "Todo recurso da 1ª instância tem efeito suspensivo. Enquanto o prazo para recorrer está aberto e, havendo recurso, até que todos tenham decisão e o resultado definitivo esteja publicado, ficam suspensos os atos que não podem ser desfeitos e o início das fases posteriores.");
        EfeitoSuspensivo.SemEfeitoSuspensivo.Descricao().Should().Be(
            "O recurso da 1ª instância não suspende nenhum ato do processo enquanto é analisado.");
        EfeitoSuspensivo.UnidadeDecideEmCadaRecurso.Descricao().Should().Be(
            "Em cada recurso da 1ª instância, essa unidade decide se concede o efeito suspensivo e registra a fundamentação. Só o recurso com a concessão suspende os atos que não podem ser desfeitos e o início das fases posteriores.");
        GuardaDaVaga.AteOResultadoDefinitivo.Descricao().Should().Be(
            "Se quem recorre seria chamado para a vaga, ela fica guardada até a publicação do resultado definitivo.");
        GuardaDaVaga.NaoGuarda.Descricao().Should().Be(
            "A vaga não fica guardada durante o recurso. Se o recurso for deferido com a vaga já ocupada, a unidade resolve a situação.");
    }

    [Fact(DisplayName = "Os códigos de contrato dos enums do domínio da Seleção passam na conferência da subida")]
    public void ConferirEnumsDe_DominioDaSelecao_Passa()
    {
        EnumsDeContrato.EhEnumQueDeclaraCodigo(typeof(TipoDeAlvoDoProcedimentoDeRecurso)).Should().BeTrue();
        FluentActions.Invoking(() => EnumsDeContrato.ConferirEnumsDe([typeof(TipoDeAlvoDoProcedimentoDeRecurso).Assembly])).Should().NotThrow();
    }
}
