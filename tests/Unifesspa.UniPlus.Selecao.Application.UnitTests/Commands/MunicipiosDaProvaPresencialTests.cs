namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class MunicipiosDaProvaPresencialTests
{
    private const FonteValoresFato Fonte = FonteValoresFato.MunicipiosProvaPresencial;
    private const string Codigo = "CIDADE_DE_PROVA";

    [Fact]
    public void Origem_FonteDaProva_MapeiaParaOCodigoDeContrato()
    {
        VocabularioDeFatos.OrigemValores(Fato()).Should().Be(OrigemValoresColeta.MunicipiosDaProvaPresencial);
        OrigemValoresColeta.MunicipiosDaProvaPresencial.Codigo().Should().Be("MUNICIPIOS_DA_PROVA_PRESENCIAL");
        VocabularioDeFatos.Classificar(Fato()).Should().Be(TipoDominioFato.CategoricoDinamico);
    }

    [Fact]
    public void DominiosDinamicos_MunicipiosDeclarados_AdmiteSomenteAOferta()
    {
        ProcessoSeletivo processo = ProcessoComMunicipios();
        DominioDeValores dominio = VocabularioDeFatos.DominiosDinamicos(processo, [Fato()])[Codigo];
        dominio.Contem("1504208").Should().BeTrue();
        dominio.Contem("1505536").Should().BeFalse();
    }

    [Fact]
    public void Resolver_MunicipiosComNomesCopiados_CongelaNaOrdemDeclarada()
    {
        ProcessoSeletivo processo = ProcessoComMunicipios();
        CopiarNomes(processo);
        ColetarCidade(processo);
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado = ResolvedorValoresSelecionaveisCongelados.Resolver(processo, new Dictionary<string, FatoCandidatoView> { [Codigo] = Fato() });
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value![Codigo]!.Select(static v => (v.Codigo, v.Descricao, v.Ordem)).Should().Equal(
            ("1504208", "Marabá/PA", 0), ("1508407", "Xinguara/PA", 1), ("1502152", "Canaã dos Carajás/PA", 2));
    }

    [Fact]
    public void Resolver_SemDeclaracao_DevolveOfertaVazia()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ColetarCidade(processo);
        Result<IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>> resultado = ResolvedorValoresSelecionaveisCongelados.Resolver(processo, new Dictionary<string, FatoCandidatoView> { [Codigo] = Fato() });
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value![Codigo].Should().BeEmpty();
        VocabularioDeFatos.DominiosDinamicos(processo, [Fato()])[Codigo].Contem("1504208").Should().BeFalse();
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessosDeTeste.SemConfiguracao("Processo de prova", OrigemCandidatos.InscricaoPropria);

    private static ProcessoSeletivo ProcessoComMunicipios()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirProvaPresencial(true, [new("1504208", "PA"), new("1508407", "PA"), new("1502152", "PA")], 10).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static void CopiarNomes(ProcessoSeletivo processo)
    {
        MunicipiosConferidosNoGeo municipios = MunicipiosConferidosNoGeo.De(new Dictionary<string, ResultadoDoMunicipioNoGeo>
        {
            ["1504208"] = ResultadoDoMunicipioNoGeo.Conferido("Marabá"),
            ["1508407"] = ResultadoDoMunicipioNoGeo.Conferido("Xinguara"),
            ["1502152"] = ResultadoDoMunicipioNoGeo.Conferido("Canaã dos Carajás"),
        });
        ProtecaoVigenteDasExigencias protecao = new(new Dictionary<string, ClassificacaoDeProtecao>(), new Dictionary<Guid, ProtecaoDoDocumento>(), new HashSet<string>(), new HashSet<Guid>());
        processo.RecopiarAoCongelar(new CopiasDoCongelamento(new Dictionary<string, CopiaDoItemNoCatalogo>(), new HashSet<string>(), new Dictionary<Guid, UnidadeResponsavelSnapshot>(), protecao, municipios)).IsSuccess.Should().BeTrue();
    }

    private static void ColetarCidade(ProcessoSeletivo processo) =>
        processo.DefinirItens([FatoColetadoDeTeste.Coletando(Codigo, 0).DoTipo(TipoDeCampo.EscolhaUnica)
            .ComOrigem(OrigemValoresColeta.MunicipiosDaProvaPresencial).Construir()], finalidade: FinalidadeFormulario.Habilitacao).IsSuccess.Should().BeTrue();

    private static FatoCandidatoView Fato() => new(
        Guid.CreateVersion7(), Codigo, "Cidade de prova", "Cidade de prova",
        CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico),
        null, "INSCRICAO", $"CAMPO_FORMULARIO:{Codigo}", null, CodigoComRotulo<FonteValoresFato>.De(Fonte), true, "PESSOAL",
        ProtecaoDoFatoDeTeste.HipoteseLegal, ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true);
}
