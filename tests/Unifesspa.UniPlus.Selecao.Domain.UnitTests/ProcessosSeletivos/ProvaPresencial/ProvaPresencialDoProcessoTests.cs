namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ProvaPresencial;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProvaPresencialDoProcessoTests
{
    [Fact]
    public void Declarar_MunicipiosEmOrdem_AtribuiPosicoesAPartirDeUm()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirProvaPresencial(true, [new("1508159", "PA"), new("1504208", "PA")], 150);
        processo.ProvaPresencial!.Municipios.Select(m => (m.CodigoIbge, m.Posicao))
            .Should().Equal(("1508159", 1), ("1504208", 2));
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void DefinirProvaPresencial_LimiteDeMunicipios_RespeitaLimite(int quantidade, bool aceito)
    {
        ProcessoSeletivo processo = NovoProcesso();
        MunicipioDeProvaInformado[] municipios = Enumerable.Range(0, quantidade).Select(i => new MunicipioDeProvaInformado($"15{i:D5}", "PA")).ToArray();
        processo.DefinirProvaPresencial(true, municipios, 150).IsSuccess.Should().Be(aceito);
    }

    [Fact]
    public void ComNomesDoGeo_MunicipioNaoConferido_MantemNomeAnterior()
    {
        DeclaracaoDaProvaPresencial declaracao = DeclaracaoDaProvaPresencial.Declarar(true, [new("1504208", "PA"), new("1508159", "PA")], 150).Value!;
        DeclaracaoDaProvaPresencial anterior = declaracao.ComNomesDoGeo(Geo(("1508159", "Xinguara")));
        DeclaracaoDaProvaPresencial atual = anterior.ComNomesDoGeo(Geo(("1504208", "Marabá")));
        atual.Municipios.Select(m => m.Nome).Should().Equal("Marabá", "Xinguara");
        declaracao.Municipios.Select(m => m.Nome).Should().OnlyContain(nome => nome == null);
    }

    [Fact]
    public void DefinirProvaPresencial_Substituicao_LimpaNomesEMantemNovaOrdem()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 150);
        processo.RecopiarAoCongelar(Copias(Geo(("1504208", "Marabá"))));
        processo.DefinirProvaPresencial(true, [new("1508159", "PA"), new("1504208", "PA")], 150);
        processo.ProvaPresencial!.Municipios.Select(m => (m.CodigoIbge, m.Nome, m.Posicao))
            .Should().Equal(("1508159", null, 1), ("1504208", null, 2));
    }

    [Fact]
    public void RecopiarAoCongelar_MunicipioConferido_CopiaNome()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirProvaPresencial(true, [new("1504208", "PA")], 150);
        processo.RecopiarAoCongelar(Copias(Geo(("1504208", "Marabá")))).IsSuccess.Should().BeTrue();
        processo.ProvaPresencial!.Municipios.Single().Nome.Should().Be("Marabá");
    }

    [Fact]
    public void Conferir_ProvaNaoDeclarada_RecusaPorDeclarar()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Result<ConfiguracaoAptaACongelar> resultado = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDeTeste.Padrao);
        resultado.Errors.Should().Contain(erro => erro.Error.Code == ProvaPresencialErrorCodes.PorDeclarar);
    }

    [Fact]
    public void DefinirProvaPresencial_ProcessoPublicado_RecusaEdicao()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        CenarioDeCongelamento cenario = ApoioDaPublicacaoDoProcessoSeletivo.CenarioDoEdital() with
        {
            Relogio = new RelogioFixo(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        };
        processo.Publicar(cenario).IsSuccess.Should().BeTrue();
        processo.DefinirProvaPresencial(false, [], 150).IsFailure.Should().BeTrue();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "Processo de seleção", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static MunicipiosConferidosNoGeo Geo(params (string Codigo, string Nome)[] municipios) =>
        MunicipiosConferidosNoGeo.De(municipios.ToDictionary(m => m.Codigo, m => ResultadoDoMunicipioNoGeo.Conferido(m.Nome)));

    private static CopiasDoCongelamento Copias(MunicipiosConferidosNoGeo municipios) => new(
        new Dictionary<string, CopiaDoItemNoCatalogo>(), new HashSet<string>(), new Dictionary<Guid, UnidadeResponsavelSnapshot>(),
        new ProtecaoVigenteDasExigencias(new Dictionary<string, ClassificacaoDeProtecao>(), new Dictionary<Guid, ProtecaoDoDocumento>(),
            new HashSet<string>(), new HashSet<Guid>()), municipios);
}
