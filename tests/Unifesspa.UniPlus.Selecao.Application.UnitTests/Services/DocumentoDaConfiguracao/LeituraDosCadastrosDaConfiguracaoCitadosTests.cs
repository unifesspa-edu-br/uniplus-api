namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os cadastros de Configuração que o documento cita são lidos em lote, uma chamada por cadastro e cada código uma vez, e o código que o cadastro não tem fica fora.</summary>
public sealed class LeituraDosCadastrosDaConfiguracaoCitadosTests
{
    private static readonly CodigosCitadosPorCadastro SemCodigos = new([], null, [], [], [], null, [], []);

    private readonly ITipoProcessoReader _tiposDeProcesso = Substitute.For<ITipoProcessoReader>();
    private readonly IFaseCanonicaReader _fasesCanonicas = Substitute.For<IFaseCanonicaReader>();
    private readonly ITipoBancaReader _tiposDeBanca = Substitute.For<ITipoBancaReader>();
    private readonly ICategoriaDocumentoReader _categorias = Substitute.For<ICategoriaDocumentoReader>();
    private readonly ITipoEtapaReader _tiposDeEtapa = Substitute.For<ITipoEtapaReader>();
    private readonly IPesoAreaEnemReader _pesosPorArea = Substitute.For<IPesoAreaEnemReader>();
    private readonly ITipoDocumentoReader _tiposDeDocumento = Substitute.For<ITipoDocumentoReader>();
    private readonly LeituraDosCadastrosDaConfiguracaoCitados _leitura;

    public LeituraDosCadastrosDaConfiguracaoCitadosTests() =>
        _leitura = new(_tiposDeProcesso, _fasesCanonicas, _tiposDeBanca, _categorias, _tiposDeEtapa, _pesosPorArea, _tiposDeDocumento);

    private static FaseCanonicaView Fase(string codigo, string origemData = "NO_EDITAL") =>
        new(Guid.CreateVersion7(), codigo, $"Fase {codigo}", null, false, true, null, true, false, false, origemData);

    private static TipoBancaView TipoCadastrado(TipoDeBanca tipo) => new(Guid.CreateVersion7(), tipo, $"Banca {tipo.Codigo()}", null, null, PapelDaBanca.ImportaCandidatos, false, false, []);

    private static CategoriaDocumentoView Categoria(string codigo) => new(Guid.CreateVersion7(), codigo, $"Categoria {codigo}", null, 1);

    private static IReadOnlyCollection<string> Lote(params string[] codigos) =>
        Arg.Is<IReadOnlyCollection<string>>(lote => lote.SequenceEqual(codigos));

    private void CadastrosComOsCodigosCitados()
    {
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("SISU", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.CreateVersion7(), "SISU", "Sistema de Seleção", null));
        _fasesCanonicas.ListarVivasPorCodigosAsync(Lote("INSCRICAO", "RECURSO"), Arg.Any<CancellationToken>()).Returns([Fase("INSCRICAO"), Fase("RECURSO")]);
        _tiposDeBanca.ListarVivosPorCodigosAsync(Lote("BANCA_ANALISE_DOCUMENTAL"), Arg.Any<CancellationToken>()).Returns([TipoCadastrado(TipoDeBanca.AnaliseDocumental)]);
        _categorias.ListarVivasPorCodigosAsync(Lote("RENDA", "IDENTIDADE"), Arg.Any<CancellationToken>()).Returns([Categoria("RENDA"), Categoria("IDENTIDADE")]);
    }

    [Fact(DisplayName = "Cada cadastro com código citado é lido numa só chamada, com os códigos distintos, e cada código volta pelo cadastro")]
    public async Task CadaCadastro_ELidoEmUmaChamadaComOsCodigosDistintos()
    {
        CadastrosComOsCodigosCitados();

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(
            SemCodigos with { TipoDeProcesso = "SISU", FasesCanonicas = ["INSCRICAO", "RECURSO"], TiposDeBanca = ["BANCA_ANALISE_DOCUMENTAL"], CategoriasDeDocumento = ["RENDA", "IDENTIDADE"] },
            CancellationToken.None);

        lidos.Value!.TiposDeProcesso.Keys.Should().Equal("SISU");
        lidos.Value.FasesCanonicas.Keys.Should().Equal("INSCRICAO", "RECURSO");
        ConferirOsTiposDeBancaLidos(lidos.Value.TiposDeBanca);
        lidos.Value.CategoriasDeDocumento.Keys.Should().Equal("RENDA", "IDENTIDADE");
        await _fasesCanonicas.Received(1).ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _tiposDeBanca.Received(1).ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _categorias.Received(1).ListarVivasPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await _tiposDeProcesso.Received(1).ObterAtivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static void ConferirOsTiposDeBancaLidos(IReadOnlyDictionary<string, TipoDeBancaCopiado> tipos)
    {
        tipos.Keys.Should().Equal("BANCA_ANALISE_DOCUMENTAL");
        tipos["BANCA_ANALISE_DOCUMENTAL"].Should().Match<TipoDeBancaCopiado>(
            c => c.Codigo == TipoDeBanca.AnaliseDocumental && c.Papel == PapelDaBanca.ImportaCandidatos && c.Nome == "Banca BANCA_ANALISE_DOCUMENTAL");
    }

    [Fact(DisplayName = "O cadastro de que o documento não cita código não é consultado")]
    public async Task CadastroSemCodigoCitado_NaoEConsultado()
    {
        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos, CancellationToken.None);

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.FasesCanonicas.Should().BeEmpty();
        await _tiposDeProcesso.DidNotReceiveWithAnyArgs().ObterAtivoPorCodigoAsync(default!, default);
        await _fasesCanonicas.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _tiposDeBanca.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
        await _categorias.DidNotReceiveWithAnyArgs().ListarVivasPorCodigosAsync(default!, default);
        await _tiposDeEtapa.DidNotReceiveWithAnyArgs().ListarAtivosPorCodigosAsync(default!, default);
        await _pesosPorArea.DidNotReceiveWithAnyArgs().ObterPorResolucaoAsync(default!, default);
    }

    [Fact(DisplayName = "O código que o cadastro não tem vivo fica fora do dicionário, sem recusar a leitura")]
    public async Task CodigoQueOCadastroNaoTem_FicaFora()
    {
        _fasesCanonicas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([Fase("INSCRICAO")]);
        _tiposDeProcesso.ObterAtivoPorCodigoAsync(default!, default).ReturnsForAnyArgs((TipoProcessoView?)null);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(
            SemCodigos with { TipoDeProcesso = "INEXISTENTE", FasesCanonicas = ["INSCRICAO", "INEXISTENTE"] }, CancellationToken.None);

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.FasesCanonicas.Keys.Should().Equal("INSCRICAO");
        lidos.Value.TiposDeProcesso.Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase canônica congela o id, o código e os traços que ela herda do cadastro")]
    public async Task FaseCanonica_CongelaOsTracosDoCadastro()
    {
        FaseCanonicaView fase = Fase("EXECUCAO", "NA_EXECUCAO");
        _fasesCanonicas.ListarVivasPorCodigosAsync(default!, default).ReturnsForAnyArgs([fase]);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { FasesCanonicas = ["EXECUCAO"] }, CancellationToken.None);

        lidos.Value!.FasesCanonicas["EXECUCAO"].Should().Be(OrigensDoTipoDeFaseDeTeste.Da(
            fase.Id, "EXECUCAO", OrigemDataFase.NaExecucao, false, true, true, false).ComNome(fase.Nome));
    }

    [Fact(DisplayName = "O tipo de processo ativo congela o id, o código e o nome")]
    public async Task TipoDeProcesso_CongelaIdCodigoENome()
    {
        TipoProcessoView tipo = new(Guid.CreateVersion7(), "INDIGENA_E_QUILOMBOLA", "Processo Seletivo Indígena e Quilombola", null);
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("INDIGENA_E_QUILOMBOLA", Arg.Any<CancellationToken>()).Returns(tipo);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TipoDeProcesso = "INDIGENA_E_QUILOMBOLA" }, CancellationToken.None);

        (lidos.Value!.TiposDeProcesso["INDIGENA_E_QUILOMBOLA"].OrigemId, lidos.Value.TiposDeProcesso["INDIGENA_E_QUILOMBOLA"].Nome).Should().Be((tipo.Id, tipo.Nome));
    }

    [Fact(DisplayName = "O tipo de processo que o domínio recusa copiar é falha da leitura, não código inexistente")]
    public async Task TipoDeProcessoQueODominioRecusa_EFalha()
    {
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("SISU", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "SISU", new string('N', 201), null));

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TipoDeProcesso = "SISU" }, CancellationToken.None);

        lidos.IsFailure.Should().BeTrue();
        lidos.Error!.Code.Should().Be("TipoProcessoSnapshot.TamanhoInvalido");
    }

    [Fact(DisplayName = "Os tipos de etapa citados são lidos numa só chamada, com os códigos distintos, e cada um volta como a cópia que a etapa guarda")]
    public async Task TiposDeEtapa_SaoLidosNumaSoChamadaEFicamPelaCopia()
    {
        TipoEtapaView redacao = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "REDACAO").Chamado("Redação").Construir();
        _tiposDeEtapa.ListarAtivosPorCodigosAsync(Lote("REDACAO", "ENTREVISTA"), Arg.Any<CancellationToken>()).Returns([redacao]);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TiposDeEtapa = ["REDACAO", "ENTREVISTA"] }, CancellationToken.None);

        lidos.Value!.TiposDeEtapa.Keys.Should().Equal("REDACAO");
        lidos.Value.TiposDeEtapa["REDACAO"].Should().Match<TipoEtapaSnapshot>(tipo => tipo.OrigemId == redacao.Id && tipo.Nome == "Redação");
        await _tiposDeEtapa.Received(1).ListarAtivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de etapa cuja cópia o domínio recusa fica fora do dicionário, como o inexistente, sem falhar a leitura")]
    public async Task TipoDeEtapaQueODominioRecusa_FicaFora()
    {
        TipoEtapaView semCarater = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "SEM_CARATER").ComCarater(admitePontuacao: false, admiteEliminacao: false).Construir();
        TipoEtapaView entrevista = TipoEtapaViewDeTeste.Com(Guid.CreateVersion7(), "ENTREVISTA").Construir();
        _tiposDeEtapa.ListarAtivosPorCodigosAsync(default!, default).ReturnsForAnyArgs([entrevista, semCarater]);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TiposDeEtapa = ["ENTREVISTA", "SEM_CARATER"] }, CancellationToken.None);

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.TiposDeEtapa.Keys.Should().Equal("ENTREVISTA");
    }

    [Fact(DisplayName = "O quadro de pesos fica pela resolução como o documento a escreveu, com os grupos e os rótulos dos grupos que faltam")]
    public async Task QuadroDePesos_FicaPelaResolucaoCitada()
    {
        ResolucaoPesoAreaEnemView cadastrada = CenarioDaClassificacao.ResolucaoCom(CenarioDaClassificacao.GruposDoAnexoI.Take(2), ("TECNOLOGICA", "Tecnológica"));
        _pesosPorArea.ObterPorResolucaoAsync("  res. 805/2024 ", Arg.Any<CancellationToken>()).Returns(cadastrada);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { ResolucaoDePesos = "  res. 805/2024 " }, CancellationToken.None);

        QuadroDePesosCitado quadro = lidos.Value!.QuadrosDePesos["  res. 805/2024 "];
        quadro.Grupos.Select(static grupo => grupo.GrupoAreaEnem.Codigo).Should().Equal("HUMANISTICA_I", "HUMANISTICA_II");
        quadro.GruposAusentes.Should().Equal("Tecnológica");
        quadro.Completo.Should().BeFalse();
        await _pesosPorArea.Received(1).ObterPorResolucaoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A resolução sem pesos cadastrados fica fora do dicionário sem falhar a leitura")]
    public async Task ResolucaoSemPesosCadastrados_FicaFora()
    {
        _pesosPorArea.ObterPorResolucaoAsync("INEXISTENTE", Arg.Any<CancellationToken>()).Returns((ResolucaoPesoAreaEnemView?)null);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { ResolucaoDePesos = "INEXISTENTE" }, CancellationToken.None);

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.QuadrosDePesos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A resolução com linha que o domínio recusa copiar fica fora do dicionário sem falhar a leitura")]
    public async Task ResolucaoComLinhaRecusada_FicaFora()
    {
        _pesosPorArea.ObterPorResolucaoAsync("RUIM", Arg.Any<CancellationToken>()).Returns(CenarioDaClassificacao.ResolucaoCom([("", "Grupo sem código")]));

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { ResolucaoDePesos = "RUIM" }, CancellationToken.None);

        lidos.IsSuccess.Should().BeTrue();
        lidos.Value!.QuadrosDePesos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo de documento é lido numa só chamada, com os códigos distintos, e congela o id, o nome, a categoria e a proteção")]
    public async Task TipoDeDocumento_ELidoEmLoteECongelaAProtecao()
    {
        TipoDocumentoView rg = TipoDocumento("RG");
        _tiposDeDocumento.ListarVivosPorCodigosAsync(Lote("RG", "CPF"), Arg.Any<CancellationToken>()).Returns([rg]);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TiposDeDocumento = ["RG", "CPF"] }, CancellationToken.None);

        lidos.Value!.TiposDeDocumento.Keys.Should().Equal("RG");
        lidos.Value.TiposDeDocumento["RG"].Should().Be(new CopiaDoTipoDeDocumento(
            rg.Id, "RG", "Documento RG", "PESSOAL", new ProtecaoDoDocumento(ClassificacaoDeProtecao.Pessoal, "Finalidade de teste", "CUMPRIMENTO_OBRIGACAO_LEGAL")));
        await _tiposDeDocumento.Received(1).ListarVivosPorCodigosAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem tipo de documento citado o cadastro não é consultado")]
    public async Task SemTipoDeDocumentoCitado_NaoConsultaOCadastro()
    {
        await _leitura.LerAsync(SemCodigos, CancellationToken.None);

        await _tiposDeDocumento.DidNotReceiveWithAnyArgs().ListarVivosPorCodigosAsync(default!, default);
    }

    private static TipoDocumentoView TipoDocumento(string codigo) =>
        new(Guid.CreateVersion7(), codigo, $"Documento {codigo}", "PESSOAL", ClassificacaoDeProtecao.Pessoal, "Finalidade de teste", "CUMPRIMENTO_OBRIGACAO_LEGAL", ProtecaoAprovada: true);
}
