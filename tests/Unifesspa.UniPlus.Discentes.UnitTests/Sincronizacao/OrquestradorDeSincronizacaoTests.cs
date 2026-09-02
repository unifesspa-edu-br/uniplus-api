namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;
using Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

public sealed class OrquestradorDeSincronizacaoTests
{
    [Fact]
    public async Task Une_as_duas_varreduras_pelo_identificador_de_origem()
    {
        // As duas varreduras se sobrepõem por construção: quem ingressou nos últimos anos e
        // continua em andamento aparece nas duas. Unir por identificador evita gravar o
        // mesmo vínculo duas vezes.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(Vinculo(1), Vinculo(2));
        origem.ResponderPorSituacao(Vinculo(2), Vinculo(3));

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        gravador.IdentificadoresGravados.Should().BeEquivalentTo([1L, 2L, 3L]);
        resumo.Repetidos.Should().Be(1, "o vínculo 2 veio nas duas varreduras");
        resumo.Inseridos.Should().Be(3);
    }

    [Fact]
    public async Task Nunca_une_por_cpf_porque_a_mesma_pessoa_pode_ter_dois_vinculos()
    {
        // Dois vínculos distintos da mesma pessoa. Reduzi-los a um apagaria justamente a
        // informação que o módulo existe para guardar.
        const string MesmoCpf = "52998224725";

        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(
            Vinculo(10, cpf: MesmoCpf),
            Vinculo(11, cpf: MesmoCpf));

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        gravador.IdentificadoresGravados.Should().BeEquivalentTo([10L, 11L]);
        resumo.Repetidos.Should().Be(0);
    }

    [Fact]
    public async Task Grava_em_lotes_do_tamanho_configurado()
    {
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso([.. Enumerable.Range(1, 7).Select(i => Vinculo(i))]);

        GravadorSimulado gravador = new();
        await Executar(origem, gravador, 3);

        gravador.TamanhosDosLotes.Should().Equal([3, 3, 1], "o resto vai num lote final");
    }

    [Fact]
    public async Task Falha_de_um_lote_nao_impede_os_seguintes_e_marca_execucao_parcial()
    {
        // Interromper deixaria a réplica tão incompleta quanto, sem a vantagem de ter
        // aproveitado o que dava.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso([.. Enumerable.Range(1, 6).Select(i => Vinculo(i))]);

        GravadorSimulado gravador = new() { FalharNoLoteDeIndice = 0 };
        ResumoDaSincronizacao resumo = await Executar(origem, gravador, 3);

        gravador.TamanhosDosLotes.Should().HaveCount(2, "o segundo lote continuou sendo tentado");
        resumo.NaoGravadosPorFalha.Should().Be(3);
        resumo.Inseridos.Should().Be(3);
        resumo.Situacao.Should().Be(SyncRunStatus.Partial);

        ContagensDaExecucao contagens = resumo.EmContagens();
        (contagens.Aproveitados + contagens.Recusados).Should().BeLessThanOrEqualTo(
            contagens.Processados,
            "o que foi aproveitado e o que foi recusado cabem no que foi tratado");
    }

    [Fact]
    public async Task Falha_na_leitura_preserva_o_que_ja_foi_gravado()
    {
        // A réplica não pode ficar com escritas que o registro da execução diz não terem
        // acontecido. As contagens valem até o ponto em que a leitura parou.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(Vinculo(1), Vinculo(2));
        origem.FalharNaSegundaVarredura = true;

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        resumo.Inseridos.Should().Be(2, "a primeira varredura chegou a gravar");
        resumo.FalhaQueInterrompeu.Should().NotBeNull();
        resumo.Situacao.Should().Be(SyncRunStatus.Partial);
    }

    [Fact]
    public async Task Falha_logo_na_primeira_leitura_nao_aproveita_nada()
    {
        OrigemSimulada origem = new() { FalharNaPrimeiraVarredura = true };

        ResumoDaSincronizacao resumo = await Executar(origem, new GravadorSimulado());

        resumo.Escritos.Should().Be(0);
        resumo.FalhaQueInterrompeu.Should().NotBeNull();
        resumo.Situacao.Should().Be(SyncRunStatus.Failed);
    }

    [Fact]
    public async Task Lote_que_falha_nao_conta_como_perdido_o_que_ja_estava_igual()
    {
        // Num lote misto, os vínculos já idênticos continuam corretos na réplica mesmo
        // quando a gravação dos demais falha. Contá-los como não gravados faria o registro
        // da execução subestimar o que ela alcançou.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso([.. Enumerable.Range(1, 4).Select(i => Vinculo(i))]);

        GravadorSimulado gravador = new()
        {
            FalharNoLoteDeIndice = 0,
            InalteradosNoLoteQueFalha = 3,
        };

        ResumoDaSincronizacao resumo = await Executar(origem, gravador, 4);

        resumo.Inalterados.Should().Be(3, "já estavam corretos na réplica");
        resumo.NaoGravadosPorFalha.Should().Be(1, "só o que precisava de escrita se perdeu");
        resumo.Situacao.Should().Be(SyncRunStatus.Partial);
    }

    [Fact]
    public async Task Execucao_sem_nada_a_escrever_e_completa()
    {
        // O caso mais comum: nenhum vínculo mudou desde ontem.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(Vinculo(1));

        GravadorSimulado gravador = new() { TratarTudoComoInalterado = true };
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        resumo.Escritos.Should().Be(0);
        resumo.Inalterados.Should().Be(1);
        resumo.Situacao.Should().Be(SyncRunStatus.Completed);
    }

    [Fact]
    public async Task Execucao_em_que_tudo_veio_fora_do_contrato_nao_e_sucesso()
    {
        // Se a origem parar de entregar um campo obrigatório, nada entra na réplica. Sem
        // esta distinção, a execução terminaria como bem-sucedida sem ter feito nada — e a
        // réplica congelaria em silêncio.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(
            Vinculo(1) with { Cpf = null },
            Vinculo(2) with { Cpf = null });

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        resumo.Escritos.Should().Be(0);
        resumo.DescartadosForaDoContrato.Should().Be(2);
        resumo.Situacao.Should().Be(SyncRunStatus.Failed);
    }

    [Fact]
    public async Task Registro_incompleto_nao_impede_a_execucao_de_ser_completa()
    {
        // Contraste com o teste acima: descarte por registro incompleto é rotina.
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(
            Vinculo(1),
            Vinculo(2) with { Curso = Vinculo(2).Curso! with { Unidade = null } });

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador);

        resumo.Inseridos.Should().Be(1);
        resumo.DescartadosPorRegistroIncompleto.Should().Be(1);
        resumo.DescartadosForaDoContrato.Should().Be(0);
        resumo.Situacao.Should().Be(SyncRunStatus.Completed);
    }

    [Fact]
    public async Task Pede_a_origem_os_recortes_que_a_configuracao_define()
    {
        OrigemSimulada origem = new();
        origem.ResponderPorIngresso(Vinculo(1));

        await Executar(origem, new GravadorSimulado());

        origem.FiltrosPedidos.Should().HaveCount(2);

        FiltroDeVinculos porIngresso = origem.FiltrosPedidos[0];
        porIngresso.Nivel.Should().Be("G");
        porIngresso.AnoIngressoMinimo.Should().Be(2026 - 10);
        porIngresso.Situacoes.Should().BeNull("esta varredura recorta por idade do vínculo");

        FiltroDeVinculos porSituacao = origem.FiltrosPedidos[1];
        porSituacao.AnoIngressoMinimo.Should().BeNull(
            "vínculo em andamento entra sem limite de idade");
        porSituacao.Situacoes.Should().BeEquivalentTo([1, 8, 9]);
    }

    [Fact]
    public async Task Corte_de_ingresso_sai_da_data_de_referencia_e_nao_do_relogio()
    {
        // Uma execução do dia 31 de dezembro pode ser processada no dia seguinte — fila
        // atrasada, nova tentativa. Se o corte viesse do relógio, ela consultaria o recorte
        // de um ano e registraria representar o outro.
        OrigemSimulada origem = new();

        await Executar(origem, new GravadorSimulado(), dataDeReferencia: new DateOnly(2026, 12, 31));

        origem.FiltrosPedidos[0].AnoIngressoMinimo.Should().Be(
            2026 - 10, "o conjunto consultado precisa ser o do dia que a execução afirma representar");
    }

    [Fact]
    public async Task Falha_ao_buscar_pagina_seguinte_grava_o_que_ja_fora_decodificado()
    {
        // Uma página é menor que um lote, então a falha pega o lote incompleto. Descartá-lo
        // faria a execução jogar fora vínculos válidos já lidos e traduzidos.
        OrigemSimulada origem = new() { FalharAoBuscarPaginaSeguinte = true };
        origem.ResponderPorIngresso(Vinculo(1), Vinculo(2), Vinculo(3));

        GravadorSimulado gravador = new();
        ResumoDaSincronizacao resumo = await Executar(origem, gravador, tamanhoDoLote: 500);

        gravador.IdentificadoresGravados.Should().BeEquivalentTo(
            [1L, 2L, 3L],
            "o lote pendente precisa ser gravado antes de a falha encerrar a varredura");
        resumo.Escritos.Should().Be(3);
    }

    private static readonly DateOnly DataDeReferenciaPadrao = new(2026, 9, 2);

    private static async Task<ResumoDaSincronizacao> Executar(
        OrigemSimulada origem,
        GravadorSimulado gravador,
        int tamanhoDoLote = 500,
        DateOnly? dataDeReferencia = null)
    {
        OrquestradorDeSincronizacao orquestrador = new(
            origem,
            gravador,
            Options.Create(new SincronizacaoOptions { TamanhoDoLote = tamanhoDoLote }),
            NullLogger<OrquestradorDeSincronizacao>.Instance);

        return await orquestrador.ExecutarAsync(
            dataDeReferencia ?? DataDeReferenciaPadrao, CancellationToken.None);
    }

    private static VinculoDiscentePayload Vinculo(int id, string cpf = "52998224725") => new()
    {
        IdDiscente = id,
        Matricula = $"2020{id:D8}",
        Cpf = cpf,
        Nome = $"DISCENTE {id}",
        Nivel = "G",
        Curso = new CursoPayload
        {
            Id = 42,
            Nome = "CIÊNCIA DA COMPUTAÇÃO",
            CodigoEmec = "1269997",
            Unidade = new UnidadePayload { Id = 12, Nome = "INSTITUTO DE CIENCIAS EXATAS" },
        },
        Situacao = new SituacaoPayload { Id = 1, Descricao = "ATIVO" },
        AnoIngresso = 2020,
        PeriodoIngresso = 1,
    };
}
