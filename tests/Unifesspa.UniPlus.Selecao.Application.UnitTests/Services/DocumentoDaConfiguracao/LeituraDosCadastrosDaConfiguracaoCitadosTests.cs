namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
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
    private readonly LeituraDosCadastrosDaConfiguracaoCitados _leitura;

    public LeituraDosCadastrosDaConfiguracaoCitadosTests() =>
        _leitura = new(_tiposDeProcesso, _fasesCanonicas, _tiposDeBanca, _categorias);

    private static FaseCanonicaView Fase(string codigo, string origemData = "NO_EDITAL") =>
        new(Guid.CreateVersion7(), codigo, $"Fase {codigo}", null, false, true, null, true, false, origemData);

    private static TipoBancaView TipoCadastrado(TipoDeBanca tipo) => new(Guid.CreateVersion7(), tipo, $"Banca {tipo.Codigo()}", null, null, PapelDaBanca.ImportaCandidatos, false, false, []);

    private static CategoriaDocumentoView Categoria(string codigo) => new(Guid.CreateVersion7(), codigo, $"Categoria {codigo}", null, 1);

    private static IReadOnlyCollection<string> Lote(params string[] codigos) =>
        Arg.Is<IReadOnlyCollection<string>>(lote => lote.SequenceEqual(codigos));

    private void CadastrosComOsCodigosCitados()
    {
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("SiSU", Arg.Any<CancellationToken>()).Returns(new TipoProcessoView(Guid.CreateVersion7(), "SiSU", "Sistema de Seleção", null));
        _fasesCanonicas.ListarVivasPorCodigosAsync(Lote("INSCRICAO", "RECURSO"), Arg.Any<CancellationToken>()).Returns([Fase("INSCRICAO"), Fase("RECURSO")]);
        _tiposDeBanca.ListarVivosPorCodigosAsync(Lote("BANCA_ANALISE_DOCUMENTAL"), Arg.Any<CancellationToken>()).Returns([TipoCadastrado(TipoDeBanca.AnaliseDocumental)]);
        _categorias.ListarVivasPorCodigosAsync(Lote("RENDA", "IDENTIDADE"), Arg.Any<CancellationToken>()).Returns([Categoria("RENDA"), Categoria("IDENTIDADE")]);
    }

    [Fact(DisplayName = "Cada cadastro com código citado é lido numa só chamada, com os códigos distintos, e cada código volta pelo cadastro")]
    public async Task CadaCadastro_ELidoEmUmaChamadaComOsCodigosDistintos()
    {
        CadastrosComOsCodigosCitados();

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(
            SemCodigos with { TipoDeProcesso = "SiSU", FasesCanonicas = ["INSCRICAO", "RECURSO"], TiposDeBanca = ["BANCA_ANALISE_DOCUMENTAL"], CategoriasDeDocumento = ["RENDA", "IDENTIDADE"] },
            CancellationToken.None);

        lidos.Value!.TiposDeProcesso.Keys.Should().Equal("SiSU");
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

        lidos.Value!.FasesCanonicas["EXECUCAO"].Should().Be(OrigensCanonicasDeTeste.ComNome(new Domain.ValueObjects.OrigemCanonicaDaFase(
            fase.Id, "EXECUCAO", OrigemDataFase.NaExecucao, false, true, true, false)
        { Nome = fase.Nome }));
    }

    [Fact(DisplayName = "O tipo de processo ativo congela o id, o código e o nome")]
    public async Task TipoDeProcesso_CongelaIdCodigoENome()
    {
        TipoProcessoView tipo = new(Guid.CreateVersion7(), "PSIQ", "Processo Seletivo Indígena e Quilombola", null);
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("PSIQ", Arg.Any<CancellationToken>()).Returns(tipo);

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TipoDeProcesso = "PSIQ" }, CancellationToken.None);

        (lidos.Value!.TiposDeProcesso["PSIQ"].OrigemId, lidos.Value.TiposDeProcesso["PSIQ"].Nome).Should().Be((tipo.Id, tipo.Nome));
    }

    [Fact(DisplayName = "O tipo de processo que o domínio recusa copiar é falha da leitura, não código inexistente")]
    public async Task TipoDeProcessoQueODominioRecusa_EFalha()
    {
        _tiposDeProcesso.ObterAtivoPorCodigoAsync("SiSU", Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), "SiSU", new string('N', 201), null));

        Result<CadastrosDaConfiguracaoCitados> lidos = await _leitura.LerAsync(SemCodigos with { TipoDeProcesso = "SiSU" }, CancellationToken.None);

        lidos.IsFailure.Should().BeTrue();
        lidos.Error!.Code.Should().Be("TipoProcessoSnapshot.TamanhoInvalido");
    }
}
