namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A definição do processo inteiro para o avaliador: as seções de todos os formulários, com os grupos
/// e os termos de cada um, sem colisão entre o que só é único dentro do formulário.
/// </summary>
public sealed class DefinicaoDoProcessoTests
{
    [Fact(DisplayName = "O mesmo código de termo em dois formulários entra como dois termos da definição")]
    public void Montar_TermoComOMesmoCodigoEmDoisFormularios_DoisTermos()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirTermos([Termo("VERACIDADE")]).IsSuccess.Should().BeTrue();
        processo.DefinirTermos([Termo("VERACIDADE")], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        DefinicaoFormulario definicao = Montar(processo);

        definicao.Termos.Select(static t => t.Codigo).Should().BeEquivalentTo(
            [DefinicaoDoProcesso.CodigoDoTermo(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Inscricao), "VERACIDADE"), DefinicaoDoProcesso.CodigoDoTermo(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao), "VERACIDADE")]);
    }

    [Fact(DisplayName = "As páginas saem na ordem do código do formulário, qualquer que seja a ordem em que os formulários chegam")]
    public void Montar_FormulariosEmQualquerOrdem_PaginasPelaOrdemDoCodigo()
    {
        ProcessoSeletivo processo = ComHabilitacao();

        DefinicaoFormulario definicao = DefinicaoDoProcesso.Montar(
            [.. processo.Formularios.OrderByDescending(static f => f.Codigo.Valor, StringComparer.Ordinal)],
            processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos, derivacoes: [], agregados: []);

        List<string> formularios = [.. definicao.Paginas.Select(static p => p.Codigo.Split(':')[0]).Distinct(StringComparer.Ordinal)];
        formularios.Should().HaveCountGreaterThan(1).And.Equal(formularios.Order(StringComparer.Ordinal));
    }

    private static GrupoColetado ComposicaoFamiliarNaSecaoFamilia() => GrupoColetadoDeTeste.Agrupando("COMPOSICAO_FAMILIAR", 0)
        .NaSecao("FAMILIA")
        .ComOcorrencias(1, null)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ComCampos(FatoColetadoDeTeste.Coletando("PARENTESCO", 0)
            .ComRotulo("Parentesco")
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir())
        .Construir();

    [Fact(DisplayName = "A seção que só tem grupo entra na definição com o grupo")]
    public void Montar_SecaoSoComGrupo_EntraComOGrupo()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens([], papel: PapelDoFormularioDeTeste.Habilitacao, grupos: [ComposicaoFamiliarNaSecaoFamilia()]).IsSuccess.Should().BeTrue();

        DefinicaoFormulario definicao = Montar(processo);

        definicao.Paginas.Single(static e => e.Codigo == DefinicaoDoProcesso.CodigoDaPagina(FormularioDeTeste.CodigoDoPapel(PapelDoFormularioDeTeste.Habilitacao), "FAMILIA"))
            .Grupos.Should().ContainSingle().Which.Codigo.Should().Be("COMPOSICAO_FAMILIAR");
    }

    [Fact(DisplayName = "O campo de texto entra na definição com o formato do fato, que a avaliação confere")]
    public void Montar_CampoDeTexto_LevaOFormato()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirItens(
            [FatoColetadoDeTeste.Coletando("CPF_RESPONSAVEL", 0)
                .ComRotulo("CPF do responsável")
                .DeTexto("CPF")
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .NaSecao("DADOS")
                .Construir()],
            papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        DefinicaoItem item = Montar(processo).Paginas.SelectMany(static e => e.Itens).Single(static i => i.FatoCodigo == "CPF_RESPONSAVEL");

        item.RestricoesDaResposta.OfType<FormatoDeTexto>().Should().ContainSingle().Which.Formato.Should().Be("CPF");
    }

    private static DefinicaoFormulario Montar(ProcessoSeletivo processo) => DefinicaoDoProcesso.Montar(
        processo.Formularios, processo.FatosColetados, processo.GruposColetados, processo.TermosExigidos, derivacoes: [], agregados: []);

    private static ProcessoSeletivo ComHabilitacao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(
            PapelDoFormularioDeTeste.Habilitacao, null, null,
            [
                PaginaDoFormulario.Criar(new PaginaEstrutura("DADOS", 0, TipoPaginaFormulario.Secao, null), "Dados", null, null).Value!,
                PaginaDoFormulario.Criar(new PaginaEstrutura("FAMILIA", 1, TipoPaginaFormulario.Secao, null), "Família", null, null).Value!,
                PaginaDoFormulario.Criar(new PaginaEstrutura("REVISAO", 2, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite), "Revisão e aceite", null, null).Value!,
            ]).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static TermoExigidoFormulario Termo(string codigo) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", FormaAceite.RegistroSemEnderecoIp, new string('a', 64)),
            null, Obrigatoriedade.Sempre).Value!;
}
