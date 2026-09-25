namespace Unifesspa.UniPlus.Portal.UnitTests.Certames;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Portal.Application.Certames;
using Unifesspa.UniPlus.Portal.Application.Certames.Origens;

public sealed class ComposicaoDaPaginaDoCertameTests
{
    private static readonly Guid ProcessoId = Guid.Parse("0192a000-0000-7000-8000-000000000001");
    private static readonly Guid Criador = Guid.Parse("0192a000-0000-7000-8000-0000000000a1");

    [Fact]
    public async Task Compoe_os_dados_do_certame_com_a_linha_do_tempo_do_mais_recente_para_o_mais_antigo()
    {
        AtoDaOrigem edital = Ato(Criador, "EDITAL", "Edital de abertura", "012/2026", new(2026, 3, 10), congela: true);
        AtoDaOrigem aviso = Ato(Guid.NewGuid(), "AVISO", "Aviso", null, new(2026, 3, 10));
        AtoDaOrigem homologacao = Ato(Guid.NewGuid(), "HOMOLOGACAO", "Homologação", "3", new(2026, 4, 2));

        ComposicaoDaPaginaDoCertame composicao = Montar(
            Encontrado(),
            new LeituraDosAtos([edital, aviso, homologacao], null));

        Result<PaginaDoCertame> resultado = await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        PaginaDoCertame pagina = resultado.Value!;
        pagina.ProcessoSeletivoId.Should().Be(ProcessoId);
        pagina.Nome.Should().Be("Medicina 2027");
        pagina.NumeroEdital.Should().Be("012/2026");
        pagina.SeloDaOrigem.Should().Be("\"1:abc\"");
        pagina.Degradacao.Should().BeNull();

        // Inverter a ordem da origem mantém o desempate dela: o aviso, registrado depois do edital
        // na mesma data, vem antes dele.
        pagina.LinhaDoTempo.Select(e => e.Id).Should().Equal(homologacao.Id, aviso.Id, edital.Id);
    }

    [Fact]
    public async Task Nao_encontrado_da_origem_e_repassado_sem_consultar_publicacoes()
    {
        RegistroFalso registro = new(new LeituraDosAtos([], null));
        ComposicaoDaPaginaDoCertame composicao = new(new ContratoFalso(new ConsultaDoCertame.NaoEncontrado()), registro);

        Result<PaginaDoCertame> resultado = await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PaginaDoCertameErrorCodes.CertameNaoEncontrado);
        registro.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task Selecao_indisponivel_nao_serve_pagina_nem_consulta_publicacoes()
    {
        RegistroFalso registro = new(new LeituraDosAtos([], null));
        ComposicaoDaPaginaDoCertame composicao = new(new ContratoFalso(new ConsultaDoCertame.Indisponivel()), registro);

        Result<PaginaDoCertame> resultado = await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PaginaDoCertameErrorCodes.OrigemIndisponivel);
        registro.Chamadas.Should().Be(0);
    }

    [Theory]
    [InlineData(FalhaNaLeituraDosAtos.OrigemIndisponivel, MotivoDaDegradacao.OrigemIndisponivel)]
    [InlineData(FalhaNaLeituraDosAtos.FalhaNaPaginacao, MotivoDaDegradacao.FalhaNaPaginacao)]
    [InlineData(FalhaNaLeituraDosAtos.TetoDePaginas, MotivoDaDegradacao.TetoDePaginas)]
    public async Task Leitura_de_atos_interrompida_degrada_mantendo_o_que_foi_lido(
        FalhaNaLeituraDosAtos falha,
        MotivoDaDegradacao esperado)
    {
        AtoDaOrigem edital = Ato(Criador, "EDITAL", "Edital de abertura", null, new(2026, 3, 10), congela: true);

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([edital], falha));

        PaginaDoCertame pagina = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None)).Value!;

        pagina.Degradacao.Should().Be(esperado);
        pagina.LinhaDoTempo.Select(e => e.Id).Should().Equal(edital.Id);
        pagina.Nome.Should().Be("Medicina 2027", "a degradação é só da linha do tempo; o certame continua servido");
    }

    [Fact]
    public async Task Linha_do_tempo_sem_o_ato_criador_da_versao_servida_degrada()
    {
        AtoDaOrigem outro = Ato(Guid.NewGuid(), "AVISO", "Aviso", null, new(2026, 3, 10));

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([outro], null));

        PaginaDoCertame pagina = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None)).Value!;

        pagina.Degradacao.Should().Be(MotivoDaDegradacao.CriadorAusente);
    }

    [Fact]
    public async Task Ato_que_cria_versao_mais_recente_que_o_criador_servido_degrada_como_detalhe_atrasado()
    {
        AtoDaOrigem edital = Ato(Criador, "EDITAL", "Edital de abertura", null, new(2026, 3, 10), congela: true);
        AtoDaOrigem retificacao = Ato(Guid.NewGuid(), "RETIFICACAO", "Retificação", null, new(2026, 3, 10), congela: true);

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([edital, retificacao], null));

        PaginaDoCertame pagina = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None)).Value!;

        pagina.Degradacao.Should().Be(
            MotivoDaDegradacao.DetalheAtrasado,
            "a retificação publicada no mesmo dia ainda não chegou ao detalhe servido");
    }

    [Fact]
    public async Task Ato_posterior_que_nao_cria_versao_nao_degrada()
    {
        AtoDaOrigem edital = Ato(Criador, "EDITAL", "Edital de abertura", null, new(2026, 3, 10), congela: true);
        AtoDaOrigem aviso = Ato(Guid.NewGuid(), "AVISO", "Aviso", null, new(2026, 3, 20));

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([edital, aviso], null));

        PaginaDoCertame pagina = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None)).Value!;

        pagina.Degradacao.Should().BeNull();
    }

    [Fact]
    public async Task Versao_anterior_ao_criador_servido_nao_degrada()
    {
        AtoDaOrigem abertura = Ato(Guid.NewGuid(), "EDITAL", "Edital de abertura", null, new(2026, 3, 10), congela: true);
        AtoDaOrigem retificacao = Ato(Criador, "RETIFICACAO", "Retificação", null, new(2026, 4, 1), congela: true);

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([abertura, retificacao], null));

        PaginaDoCertame pagina = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None)).Value!;

        pagina.Degradacao.Should().BeNull("o detalhe já serve a versão mais recente");
    }

    [Theory]
    [InlineData("Edital de abertura", "015", "Edital de abertura nº 015", "015")]
    [InlineData("Edital de abertura", "012/2026", "Edital de abertura nº 012/2026", "012/2026")]
    [InlineData("Edital de retificação", "012/2026-R1", "Edital de retificação nº 012/2026-R1", "012/2026-R1")]
    [InlineData("Edital de abertura", " 015 ", "Edital de abertura nº 015", "015")]
    [InlineData("Edital de abertura", null, "Edital de abertura", null)]
    [InlineData("Edital de abertura", "   ", "Edital de abertura", null)]
    [InlineData(null, "015", "EDITAL nº 015", "015")]
    public async Task Titulo_do_evento_e_o_nome_do_tipo_com_o_numero_declarado_sem_compor_o_ano(
        string? tipoNome,
        string? numero,
        string tituloEsperado,
        string? numeroEsperado)
    {
        // Ano da numeração diferente do ano da data: o evento carrega o declarado, não o derivado.
        AtoDaOrigem ato = new(Criador, "EDITAL", tipoNome, 2026, numero, new(2027, 3, 10), true, null);

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([ato], null));

        EventoDaLinhaDoTempo evento = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None))
            .Value!.LinhaDoTempo.Single();

        evento.Titulo.Should().Be(tituloEsperado);
        evento.Numero.Should().Be(numeroEsperado);
        evento.Ano.Should().Be(2026);
        evento.Tipo.Should().Be(new TipoDoEvento("EDITAL", tipoNome ?? "EDITAL"));
    }

    [Fact]
    public async Task Descricao_do_evento_e_o_motivo_da_retificacao()
    {
        AtoDaOrigem retificacao = new(
            Criador, "RETIFICACAO", "Retificação", 2026, null, new(2026, 4, 1), true, "Correção do cronograma");

        ComposicaoDaPaginaDoCertame composicao = Montar(Encontrado(), new LeituraDosAtos([retificacao], null));

        EventoDaLinhaDoTempo evento = (await composicao.ComporPorIdAsync(ProcessoId, CancellationToken.None))
            .Value!.LinhaDoTempo.Single();

        evento.Descricao.Should().Be("Correção do cronograma");
        evento.Data.Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public async Task Consulta_pelo_identificador_legivel_usa_o_mesmo_contrato()
    {
        ContratoFalso contrato = new(Encontrado());
        ComposicaoDaPaginaDoCertame composicao = new(
            contrato,
            new RegistroFalso(new LeituraDosAtos([Ato(Criador, "EDITAL", "Edital", null, new(2026, 3, 10), congela: true)], null)));

        Result<PaginaDoCertame> resultado = await composicao.ComporPorIdentificadorAsync("medicina-2027", CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        contrato.IdentificadorPedido.Should().Be("medicina-2027");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("Medicina-2027")]
    [InlineData("medicina 2027")]
    [InlineData("ab")]
    public async Task Identificador_fora_do_formato_e_nao_encontrado_sem_consultar_a_origem(string identificador)
    {
        ContratoFalso contrato = new(Encontrado());
        ComposicaoDaPaginaDoCertame composicao = new(contrato, new RegistroFalso(new LeituraDosAtos([], null)));

        Result<PaginaDoCertame> resultado = await composicao.ComporPorIdentificadorAsync(identificador, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(PaginaDoCertameErrorCodes.CertameNaoEncontrado);
        contrato.IdentificadorPedido.Should().BeNull();
    }

    [Fact]
    public void Nenhum_tipo_da_pagina_carrega_o_assinante_do_ato()
    {
        Type[] tipos = [typeof(AtoDaOrigem), typeof(EventoDaLinhaDoTempo), typeof(PaginaDoCertame), typeof(TipoDoEvento)];

        tipos.SelectMany(t => t.GetProperties()).Select(p => p.Name)
            .Should().NotContain(nome => nome.Contains("Assinante", StringComparison.OrdinalIgnoreCase));
    }

    private static ConsultaDoCertame.Encontrado Encontrado() =>
        new ConsultaDoCertame.Encontrado(new CertameDaOrigem(ProcessoId, Criador, "Medicina 2027", "012/2026", "\"1:abc\""));

    private static AtoDaOrigem Ato(
        Guid id,
        string tipoCodigo,
        string? tipoNome,
        string? numero,
        DateOnly data,
        bool congela = false) =>
        new(id, tipoCodigo, tipoNome, data.Year, numero, data, congela, null);

    private static ComposicaoDaPaginaDoCertame Montar(ConsultaDoCertame consulta, LeituraDosAtos leitura) =>
        new(new ContratoFalso(consulta), new RegistroFalso(leitura));

    private sealed class ContratoFalso(ConsultaDoCertame resposta) : IContratoPublicoDoCertame
    {
        public string? IdentificadorPedido { get; private set; }

        public Task<ConsultaDoCertame> ObterPorIdAsync(Guid processoSeletivoId, CancellationToken cancellationToken) =>
            Task.FromResult(resposta);

        public Task<ConsultaDoCertame> ObterPorIdentificadorAsync(string identificador, CancellationToken cancellationToken)
        {
            IdentificadorPedido = identificador;
            return Task.FromResult(resposta);
        }
    }

    private sealed class RegistroFalso(LeituraDosAtos leitura) : IRegistroDeAtosDoCertame
    {
        public int Chamadas { get; private set; }

        public Task<LeituraDosAtos> LerAtosAsync(Guid processoSeletivoId, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.FromResult(leitura);
        }
    }
}
