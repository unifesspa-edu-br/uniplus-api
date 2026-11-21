namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ApoioDasBancasDoProcesso;

/// <summary>
/// O mínimo de integrantes da banca, a partir do número de corretores e do quórum, sobre o estado do processo: a mesma pendência
/// alimenta a conferência de congelamento e o painel.
/// </summary>
public sealed class BancasDoProcessoTests
{
    [Fact(DisplayName = "Redação com três corretores e banca de 2 titulares com quórum 2 é recusada, citando mínimo e valor declarado")]
    public void BancaCurta_ParaTresCorretores_Recusa()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);

        DomainError? pendencia = Pendencia(processo, Corretores((CorrecaoIndependente, 3)));

        pendencia!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        pendencia.Message.Should().Contain("declara 2 integrantes").And.Contain("o mínimo exigido é 3");
    }

    [Theory(DisplayName = "Banca da etapa aceita quando integrantes alcançam o maior entre o quórum e os corretores")]
    [InlineData(3, 2, 3)]
    [InlineData(5, 4, 3)]
    [InlineData(3, 2, 0)]
    [InlineData(1, 1, 1)]
    public void BancaDaEtapa_NoMinimo_Aceita(int integrantes, int quorum, int corretores)
    {
        EtapaProcesso etapa = ComRecurso(NovaEtapa("Redação", 1, corretores == 0 ? null : CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([DeRecursosDaEtapa(integrantes, quorum)]);

        Pendencia(processo, Corretores((CorrecaoIndependente, corretores))).Should().BeNull();
    }

    [Theory(DisplayName = "Banca da etapa de maioria simples com novo corretor exige os três corretores e mais um, e aceita quando os alcança")]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void BancaDaEtapa_ComNovoCorretor_ExigeOsCorretoresMaisUm(int integrantes, bool aceita)
    {
        EtapaProcesso etapa = ComRecurso(NovaEtapa("Entrevista", 1, MaioriaSimples));
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([DeRecursosDaEtapa(integrantes, quorum: 2)]);

        DomainError? pendencia = Pendencia(processo, Corretores((MaioriaSimples, 4)));

        (pendencia is null).Should().Be(aceita);
        pendencia?.Message.Should().Contain($"declara {integrantes} integrantes").And.Contain("o mínimo exigido é 4");
    }

    [Fact(DisplayName = "Quórum maior que os corretores eleva o mínimo ao quórum")]
    public void Minimo_ComQuorumMaiorQueOsCorretores_ExigeOQuorum()
    {
        EtapaProcesso redacao = NovaEtapa("Redação", 1, CorrecaoIndependente);

        MinimoDeIntegrantes.Exigido(BancasDeTeste.Regra(integrantes: 5, quorum: 4), [Corretores((CorrecaoIndependente, 3)).EmCampo(redacao)])
            .Should().Be(4);
    }

    [Fact(DisplayName = "Banca da fase que julga uma etapa de avaliação única e outra de correção independente usa o maior número de corretores")]
    public void BancaDaFase_JulgandoDuasEtapas_UsaOMaiorNumeroDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 2, quorum: 2)], redacao, entrevista);

        DomainError? pendencia = Pendencia(processo, Corretores((CorrecaoIndependente, 3), (AvaliacaoUnica, 1)));

        pendencia!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        pendencia.Message.Should().Contain("o mínimo exigido é 3");
    }

    [Fact(DisplayName = "Banca da etapa julga só a própria etapa, mesmo que outra exija mais corretores")]
    public void BancaDaEtapa_JulgaSoAPropriaEtapa()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 3, quorum: 2)], redacao, entrevista);
        processo.Etapas.Single(e => e.Nome == "Entrevista").DefinirBancas([DeRecursosDaEtapa(integrantes: 1, quorum: 1)]);

        Pendencia(processo, Corretores((CorrecaoIndependente, 3), (AvaliacaoUnica, 1))).Should().BeNull();
    }

    [Fact(DisplayName = "Banca da etapa que não é de recursos não julga a etapa e não herda o mínimo de corretores")]
    public void BancaDaEtapaQueNaoEDeRecursos_NaoHerdaOMinimoDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas([BancasDeTeste.DeRecursosDaFase()], redacao);
        redacao.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Tipo), BancasDeTeste.Regra(integrantes: 2, quorum: 2))]);

        Pendencia(processo, Corretores((CorrecaoIndependente, 3))).Should().BeNull();
    }

    [Fact(DisplayName = "O painel projeta a mesma pendência da conferência em cada item das bancas")]
    public void Painel_ProjetaAPendenciaDaConferencia()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);
        CorretoresDasEtapas corretores = Corretores((CorrecaoIndependente, 3));

        IEnumerable<string> vermelhos = Itens(processo, corretores)
            .Where(static item => !item.Ok).Select(static item => item.Codigo);

        vermelhos.Should().Equal("cronograma_banca_com_o_minimo_de_integrantes");
        Itens(processo, Corretores((CorrecaoIndependente, 2))).Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "Sem consolidação humana, o painel aprova a banca com dois integrantes sem corretores")]
    public void SemConsolidacao_PainelAceitaSemCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);

        CorretoresDeTeste.SemConsolidacao.EmCampo(redacao).Should().Be(0);
        Itens(processo, CorretoresDeTeste.SemConsolidacao).Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "Etapa com regra de consolidação não resolvida gera recusa própria, citando a etapa, e nunca vale zero corretores")]
    public void EtapaComRegraNaoResolvida_RecusaSemPresumirNumero()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]);

        DomainError? pendencia = Pendencia(processo, CorretoresDeTeste.SemConsolidacao);

        pendencia!.Code.Should().Be("EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        pendencia.Message.Should().Contain("julga a etapa na posição 1").And.NotContain(CorrecaoIndependente.Hash);
    }

    [Fact(DisplayName = "A etapa resolvida e a não resolvida julgadas pela mesma banca acumulam o mínimo e a regra não resolvida")]
    public void EtapaResolvidaENaoResolvida_AcumulamAsDuasRecusas()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 2, quorum: 2)], redacao, entrevista);

        IReadOnlyList<FieldError> violacoes = Minimo(processo, Corretores((CorrecaoIndependente, 3)));

        violacoes.Select(static erro => erro.Error.Code).Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        violacoes[^1].Error.Message.Should().Contain("julga a etapa na posição 2");
    }

    [Fact(DisplayName = "A banca abaixo do mínimo vem antes da regra não resolvida de outra banca que a precede")]
    public void BancaAbaixoDoMinimo_VemAntesDaRegraNaoResolvidaDeBancaAnterior()
    {
        ReferenciaRegra lida = ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v2", new string('f', 64));
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, lida));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 3, quorum: 2)], redacao, entrevista);
        entrevista.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);
        CorretoresDasEtapas corretores = Corretores((lida, 3));

        IEnumerable<string> violacoes = Minimo(processo, corretores).Select(static erro => erro.Error.Code);

        violacoes.Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        Pendencia(processo, corretores)!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        CodigosDaConferencia(processo, corretores).Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
    }

    [Fact(DisplayName = "A conferência aceita a banca com dois integrantes sem corretores quando a etapa não declara consolidação")]
    public void Conferencia_SemConsolidacaoAceitaSemCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);

        CorretoresDeTeste.SemConsolidacao.EmCampo(redacao).Should().Be(0);
        CodigosDaConferencia(processo, CorretoresDeTeste.SemConsolidacao).Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação julgada pela banca não gera recusa de regra não resolvida")]
    public void EtapaSemRegra_NaoGeraRecusaDeRegraNaoResolvida()
    {
        EtapaProcesso objetiva = ComRecurso(NovaEtapa("Objetiva", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, objetiva);
        objetiva.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]);

        Pendencia(processo, CorretoresDeTeste.SemConsolidacao).Should().BeNull();
    }

    [Fact(DisplayName = "O painel deixa vermelho o item do mínimo com a regra não resolvida, sem lançar")]
    public void Painel_ComRegraNaoResolvida_DeixaOItemVermelho()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]);

        IEnumerable<string> vermelhos = Itens(processo, CorretoresDeTeste.SemConsolidacao)
            .Where(static item => !item.Ok).Select(static item => item.Codigo);

        vermelhos.Should().Equal("cronograma_banca_com_o_minimo_de_integrantes");
    }

    [Fact(DisplayName = "A conferência lê os corretores dos cadastros")]
    public void Conferencia_LeOsCorretoresDosCadastros()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]);

        CodigosDaConferencia(processo, Corretores((CorrecaoIndependente, 2))).Should().BeEmpty();
        CodigosDaConferencia(processo, Corretores((CorrecaoIndependente, 3))).Should().Equal("Banca.IntegrantesAbaixoDoMinimo");
        CodigosDaConferencia(processo, CorretoresDeTeste.SemConsolidacao).Should().Equal("EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
    }

    [Fact(DisplayName = "Redefinir as bancas da etapa com o id da banca reaproveita a linha e leva a ela a origem, o nome e a regra declarados")]
    public void Redefinir_ComOId_ReaproveitaALinhaEAlinhaOsDadosDeclarados()
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        etapa.DefinirBancas([BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Tipo), BancasDeTeste.Regra(3, 2))]);
        Guid linha = etapa.Bancas.Single().Id;
        Guid outraOrigem = Guid.CreateVersion7();

        etapa.DefinirBancas(
            [BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.Nomeada(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, outraOrigem), "Novo"), BancasDeTeste.Regra(5, 4), idDaBancaQueSubstitui: linha)]);

        BancaDaEtapa banca = etapa.Bancas.Single();
        banca.Id.Should().Be(linha);
        banca.Tipo.OrigemId.Should().Be(outraOrigem);
        banca.Tipo.Nome.Should().Be("Novo");
        banca.RegraDeDeliberacao!.Integrantes.Should().Be(5);
    }
}
