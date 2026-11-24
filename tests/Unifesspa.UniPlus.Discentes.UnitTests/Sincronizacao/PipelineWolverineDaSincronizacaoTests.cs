namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;

/// <summary>
/// Exercita o handler da sincronização pelo Wolverine real.
/// </summary>
/// <remarks>
/// O gerador de código do Wolverine monta o pipeline na subida. Se faltar a declaração de
/// que alguma porta do módulo vem do contêiner, é aqui que a montagem falha — e não em
/// tempo de execução, na primeira sincronização de madrugada. Testes com substitutos
/// diretos do handler não passam por esse caminho.
/// </remarks>
public sealed class PipelineWolverineDaSincronizacaoTests
{
    private static readonly DateOnly Dia = new(2026, 9, 2);

    [Fact]
    public async Task Wolverine_monta_o_pipeline_e_despacha_a_sincronizacao()
    {
        ExecucoesEmMemoria execucoes = await SincronizarComSucesso();

        execucoes.Registradas.Should().ContainSingle();
    }

    [Fact]
    public async Task Sincronizacao_despachada_termina_concluida()
    {
        ExecucoesEmMemoria execucoes = await SincronizarComSucesso();

        execucoes.Registradas[0].Status.Should().Be(SyncRunStatus.Concluida);
    }

    [Fact]
    public async Task Sincronizacao_despachada_guarda_a_data_de_referencia_pedida()
    {
        ExecucoesEmMemoria execucoes = await SincronizarComSucesso();

        execucoes.Registradas[0].DataDeReferencia.Should().Be(Dia);
    }

    [Fact]
    public async Task Falha_da_origem_chega_a_quem_pediu_a_sincronizacao()
    {
        (_, Exception? falha) = await SincronizarComOrigemQueFalha();

        falha.Should().NotBeNull();
    }

    [Fact]
    public async Task Execucao_que_falha_fica_registrada_uma_unica_vez()
    {
        (ExecucoesEmMemoria execucoes, _) = await SincronizarComOrigemQueFalha();

        execucoes.Registradas.Should().ContainSingle();
    }

    [Fact]
    public async Task Execucao_que_falha_fica_registrada_como_fracassada()
    {
        // Sem isto, uma falha deixaria a execução presa em andamento para sempre — e é ela
        // que a próxima execução consulta para saber se o dia já foi tratado.
        (ExecucoesEmMemoria execucoes, _) = await SincronizarComOrigemQueFalha();

        execucoes.Registradas[0].Status.Should().Be(SyncRunStatus.Falhou);
    }

    [Fact]
    public async Task Execucao_que_falha_fica_com_o_instante_de_termino_registrado()
    {
        (ExecucoesEmMemoria execucoes, _) = await SincronizarComOrigemQueFalha();

        execucoes.Registradas[0].FinishedAt.Should().NotBeNull();
    }

    private static async Task<ExecucoesEmMemoria> SincronizarComSucesso()
    {
        await using HospedeWolverineDaSincronizacao hospede = await HospedeWolverineDaSincronizacao.SubirAsync();

        await hospede.SincronizarAsync(Dia);

        return hospede.Execucoes;
    }

    private static async Task<(ExecucoesEmMemoria Execucoes, Exception? Falha)> SincronizarComOrigemQueFalha()
    {
        await using HospedeWolverineDaSincronizacao hospede =
            await HospedeWolverineDaSincronizacao.SubirAsync(origemQueFalha: true);

        Exception? falha = await Record.ExceptionAsync(() => hospede.SincronizarAsync(Dia));

        return (hospede.Execucoes, falha);
    }
}
