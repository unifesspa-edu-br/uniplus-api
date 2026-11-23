namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProjecaoDaProvaPresencialTests
{
    [Fact]
    public void Projetar_PorDeclarar_DevolveNull() =>
        ProjecaoDoProcessoSeletivo.Projetar(Processo()).ProvaPresencial.Should().BeNull();

    [Fact]
    public void Projetar_SemProva_DevolveDeclaracaoVazia()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirProvaPresencial(false, [], 100).IsSuccess.Should().BeTrue();
        ProvaPresencialDto prova = ProjecaoDoProcessoSeletivo.Projetar(processo).ProvaPresencial!;
        prova.TemProvaPresencial.Should().BeFalse();
        prova.Municipios.Should().BeEmpty();
    }

    [Fact]
    public void Projetar_ComMunicipios_PreservaOrdemENomesCopiados()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirProvaPresencial(true, [new("1508159", "PA"), new("1504208", "PA")], 100).IsSuccess.Should().BeTrue();
        ProjecaoDoProcessoSeletivo.Projetar(processo).ProvaPresencial!.Municipios.Should().Equal(
            new MunicipioDeProvaDto("1508159", "PA", null), new MunicipioDeProvaDto("1504208", "PA", null));
        CopiarNome(processo);
        ProjecaoDoProcessoSeletivo.Projetar(processo).ProvaPresencial!.Municipios.Should().Equal(
            new MunicipioDeProvaDto("1508159", "PA", "Xinguara"), new MunicipioDeProvaDto("1504208", "PA", null));
    }

    private static ProcessoSeletivo Processo() => ProcessosDeTeste.SemConfiguracao("Processo de prova", OrigemCandidatos.InscricaoPropria);

    private static void CopiarNome(ProcessoSeletivo processo)
    {
        MunicipiosConferidosNoGeo geo = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            ["1508159"] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
        });
        ProtecaoVigenteDasExigencias protecao = new(new Dictionary<string, ClassificacaoDeProtecao>(),
            new Dictionary<Guid, ProtecaoDoDocumento>(), new HashSet<string>(), new HashSet<Guid>());
        processo.RecopiarAoCongelar(new CopiasDoCongelamento(new Dictionary<string, CopiaDoItemNoCatalogo>(),
            new HashSet<string>(), new Dictionary<Guid, UnidadeResponsavelSnapshot>(), protecao, geo)).IsSuccess.Should().BeTrue();
    }
}
